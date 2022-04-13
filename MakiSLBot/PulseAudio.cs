using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace MakiSLBot;

public class PulseAudio
{
    private string slVoiceDir;
    private string? slVoiceWrapperPath;
    
    private int pulseAudioPort;
    private ManualResetEvent pulseAudioLoopSignal = new ManualResetEvent(false);
    private Process? pulseAudioProcess;

    private static int GetAvailablePort()
    {
        int port;
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            var localEp = new IPEndPoint(IPAddress.Any, 0);
            socket.Bind(localEp);
            port = ((IPEndPoint)socket.LocalEndPoint!).Port;
        }
        finally
        {
            socket.Close();
        }
        return port;
    }
    
    private void WriteSlVoiceWrapper()
    {
        var dirNextToExe = Path.GetDirectoryName(
            (System.Reflection.Assembly.GetEntryAssembly() ?? typeof (PulseAudio).Assembly).Location
        );

        if (dirNextToExe == null)
        {
            throw new Exception("Can't get directory next to executable");
        }

        slVoiceWrapperPath = Path.Combine(dirNextToExe, "SLVoice");

        pulseAudioPort = GetAvailablePort();

        var shFile = new []
        {
            "#!/bin/sh",
            "# THIS FILE WAS AUTO GENERATED AT RUNTIME",
            "export WINEDEBUG=-all",
            $"export PULSE_SERVER=tcp:127.0.0.1:{pulseAudioPort}",
            $"exec /usr/bin/padsp wine64 {Path.Join(slVoiceDir, "SLVoice.exe").Replace(" ", "\\ ")} \"$@\"\n"
        };
        
        File.WriteAllText(slVoiceWrapperPath, string.Join('\n', shFile));

        Process.Start("chmod", new [] { "+x", slVoiceWrapperPath });
    }

    private void StartPulseAudio()
    {
        StopPulseAudio();
        pulseAudioLoopSignal.Set();

        var thread = new Thread(delegate()
        {
            while (pulseAudioLoopSignal.WaitOne(500, false))
            {
                var args = new[]
                {
                    "-n", // dont use default config
                    "--exit-idle-time=-1", // dont close on idle
                    // pactl list reports SLVoice.exe uses float32le 1ch 32000Hz
                    "-L \"module-null-sink sink_name=auto_null format=float32le channels=1 rate=32000\"",
                    $"-L \"module-native-protocol-tcp listen=127.0.0.1 port={pulseAudioPort}\"" // open tcp server
                };

                pulseAudioProcess = new Process
                {
                    StartInfo =
                    {
                        FileName = "pulseaudio",
                        Arguments = string.Join(' ', args),
                        // RedirectStandardInput = true,
                        // RedirectStandardOutput = true,
                        // RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                    }
                };

                var ok = pulseAudioProcess.Start();

                if (!ok)
                {
                    pulseAudioLoopSignal.Reset();
                    return;
                }

                Console.WriteLine($"Started PulseAudio on PULSE_SERVER=tcp:127.0.0.1:{pulseAudioPort}");
                
                pulseAudioProcess.WaitForExit();
            }
        });
        
        thread.Start();
    }

    private void StopPulseAudio()
    {
        try
        {
            pulseAudioLoopSignal.Reset();
            pulseAudioProcess?.Kill();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public PulseAudio(string slVoiceDir)
    {
        if (Environment.OSVersion.Platform != PlatformID.Unix)
        {
            throw new Exception("Can't run PulseAudio on Windows");
        }
            
        this.slVoiceDir = slVoiceDir;
        WriteSlVoiceWrapper();
        StartPulseAudio();
    }

    public void Cleanup()
    {
        StopPulseAudio();
        if (File.Exists(slVoiceWrapperPath)) File.Delete(slVoiceWrapperPath);
    }

    // public async Task PlayWavAudioFile(string audioFilePath)
    // {
    //     var process = Process.Start(
    //         "paplay",
    //         $"-s tcp:127.0.0.1:{pulseAudioPort} {audioFilePath.Replace(" ", "\\ ")}"
    //     );
    //     await process.WaitForExitAsync();
    // }

    private readonly IDictionary<string, string> whichCache = new Dictionary<string, string>();
    private async Task<string> Which(string command)
    {
        if (whichCache.TryGetValue(command, out var path)) return path;
        var startInfo = new ProcessStartInfo
        {
            FileName = "which",
            Arguments = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        };
        var process = Process.Start(startInfo);
        if (process == null) return "";
        await process.WaitForExitAsync();
        path = await process.StandardOutput.ReadLineAsync();
        if (path == null) return "";
        whichCache.Add(command, path);
        return path;
    }
    
    public async Task PlayAudioFile(byte[] audioFileBytes, int volume = 100)
    {
        Console.WriteLine(await Which("ffplay"));
        var startInfo = new ProcessStartInfo
        {
            FileName = await Which("ffplay"),
            Arguments = $"-autoexit -nodisp -volume {volume} -",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false, // necessary for stdin
        };
        startInfo.Environment.Add("PULSE_SERVER", $"tcp:127.0.0.1:{pulseAudioPort}");

        var process = Process.Start(startInfo);
        if (process == null) return;
        
        process.StandardInput.BaseStream.Write(audioFileBytes);
        await process.StandardInput.BaseStream.FlushAsync();
        process.StandardInput.BaseStream.Close();
        
        await process.WaitForExitAsync();
        
        var error = await process.StandardError.ReadToEndAsync();
        if (error != "") Console.WriteLine(error);
    }

    public void TextToSpeech(string voice, string message)
    {
        Task.Run(async () =>
        {
            const string sayServer = "https://say.cutelab.space";
            var audioUrl = new Uri(
                $"{sayServer}/sound.wav?voice={Uri.EscapeDataString(voice)}&text={Uri.EscapeDataString(message)}"
            );

            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(audioUrl);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync();
            await PlayAudioFile(bytes, 40);
        });
    }
}