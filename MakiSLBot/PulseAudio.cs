using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace MakiSLBot;

public class PulseAudio
{
    private string slVoiceDir;
    private string slVoiceWrapperPath;
    
    private int pulseAudioPort;
    private ManualResetEvent pulseAudioLoopSignal = new ManualResetEvent(false);
    private Process pulseAudioProcess;

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
            $"export PULSE_SERVER=tcp:127.0.0.1:{pulseAudioPort}",
            $"export LD_LIBRARY_PATH={slVoiceDir.Replace(" ", "\\ ")}",
            $"exec /usr/bin/padsp {Path.Join(slVoiceDir, "SLVoice").Replace(" ", "\\ ")} \"$@\"\n"
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
                    // pactl list reports SLVoice uses s16le 1ch 32000Hz
                    "-L \"module-null-sink sink_name=auto_null format=s16le channels=1 rate=32000\"",
                    $"-L \"module-native-protocol-tcp listen=127.0.0.1 port={pulseAudioPort}\"" // open tcp server
                };

                pulseAudioProcess = new Process();
                pulseAudioProcess.StartInfo.FileName = "pulseaudio";
                pulseAudioProcess.StartInfo.Arguments = string.Join(' ', args);
                // hides messages to console
                // pulseAudioProcess.StartInfo.RedirectStandardOutput = true;
                // pulseAudioProcess.StartInfo.RedirectStandardError = true;

                var ok = pulseAudioProcess.Start();

                if (!ok)
                {
                    pulseAudioLoopSignal.Reset();
                    return;
                }

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
            pulseAudioProcess.Kill();
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

    public async Task PlayWavAudioFile(string audioFilePath)
    {
        var process = Process.Start(
            "paplay",
            $"-s tcp:127.0.0.1:{pulseAudioPort} {audioFilePath}"
        );
        await process.WaitForExitAsync();
    }

    public void TextToSpeech(string voice, string message)
    {
        Task.Run(async () =>
        {
            var audioFilePath = Path.GetTempFileName();

            const string sayServer = "https://say.cutelab.space";
            var audioUrl = new Uri(
                $"{sayServer}/sound.wav?voice={Uri.EscapeDataString(voice)}&text={Uri.EscapeDataString(message)}"
            );

            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(audioUrl);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(audioFilePath, bytes);

            await PlayWavAudioFile(audioFilePath);
            
            File.Delete(audioFilePath);
        });
    }
}