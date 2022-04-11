#define DEBUG

using System.Text.RegularExpressions;
using LibreMetaverse.Voice;
using OpenMetaverse;

namespace MakiSLBot;

public class MakiSLBot
{
    private readonly GridClient client;
    private readonly VoiceGateway voiceGateway;
    private readonly PulseAudio pulseAudio;
    
    private void VoiceGatewayOnAuxGetCaptureDevicesResponse(object? sender, VoiceGateway.VoiceDevicesEventArgs e)
    {
        // foreach (var device in e.Devices)
        // {
        //     Console.WriteLine(device);
        // }
        voiceGateway.CurrentCaptureDevice = "Default System Device";
    }

    private void VoiceGatewayOnAuxGetRenderDevicesResponse(object? sender, VoiceGateway.VoiceDevicesEventArgs e)
    {
        // foreach (var device in e.Devices)
        // {
        //     Console.WriteLine(device);
        // }
        voiceGateway.CurrentCaptureDevice = "Default System Device";
    }
    
    private void VoiceGatewayOnSessionCreate(object? sender, EventArgs e)
    {
        Console.WriteLine("OnSessionCreate");
        voiceGateway.AuxGetCaptureDevices();
        voiceGateway.AuxGetRenderDevices();
        
        VoiceSession? session = sender as VoiceSession;
        
        voiceGateway.MicMute = false;
        voiceGateway.MicLevel = 25;

        voiceGateway.SpkrMute = true;
        voiceGateway.SpkrLevel = 0;
    }
    
    private void VoiceGatewayOnSessionRemove(object? sender, EventArgs e)
    {

    }

    public MakiSLBot()
    {
        Settings.LOG_LEVEL = Helpers.LogLevel.Warning;
        
        client = new GridClient();
        client.Self.Movement.Fly = false;
        client.Settings.MULTIPLE_SIMS = false;
        
        client.Settings.LOG_RESENDS = false;
        client.Settings.STORE_LAND_PATCHES = true;
        client.Settings.ALWAYS_DECODE_OBJECTS = true;
        client.Settings.ALWAYS_REQUEST_OBJECTS = true;
        client.Settings.SEND_AGENT_UPDATES = true;
        
        client.Self.ChatFromSimulator += ChatFromSimulator;
        
        client.Network.EventQueueRunning += ClientOnEventQueueRunning;

        var spawn = (Config.Get("SPAWN") ?? "").Split(' ');
        var startLocation = spawn.Length >= 4 ? 
            NetworkManager.StartLocation(spawn[0], int.Parse(spawn[1]), int.Parse(spawn[2]), int.Parse(spawn[3])) : 
            NetworkManager.StartLocation("Helios", 0, 0, 0); 
        
        if (!client.Network.Login(
            Config.Get("USERNAME"), "Resident", Config.Get("PASSWORD"),
            "ItsDotNetAlright", startLocation, "maki.cafe")
        )
        {
            throw new Exception("Failed to login: " + client.Network.LoginMessage);
        }
        
        Console.WriteLine("Logged in: " + client.Network.LoginMessage);

        Console.WriteLine("Starting voice gateway");

        var slVoiceDir = Config.Get("SL_VOICE_DIR");
        if (slVoiceDir == null)
        {
            Console.WriteLine("SL_VOICE_DIR not provided, voice won't start");
        }
        else
        {
            pulseAudio = new PulseAudio(slVoiceDir);
        
            voiceGateway = new VoiceGateway(client);
            voiceGateway.OnSessionCreate += VoiceGatewayOnSessionCreate;
            voiceGateway.OnSessionRemove += VoiceGatewayOnSessionRemove;
            voiceGateway.OnAuxGetCaptureDevicesResponse += VoiceGatewayOnAuxGetCaptureDevicesResponse;
            voiceGateway.OnAuxGetRenderDevicesResponse += VoiceGatewayOnAuxGetRenderDevicesResponse;
            voiceGateway.Start();
        }
        
    }

    public void Cleanup()
    {
        Console.WriteLine("Shutting down and cleaning up...");
        try { voiceGateway.Stop(); } catch (Exception) {}
        try { client.Network.Logout(); } catch (Exception) {}
        try { pulseAudio.Cleanup(); } catch (Exception) {}
    }

    private void ClientOnEventQueueRunning(object? sender, EventQueueRunningEventArgs e)
    {
        Console.WriteLine("Event queue running");
    }
    private void WalkTowards(Vector3 to)
    {
        var from = client.Self.SimPosition;
        var dir = Vector3.Normalize(to - from);
        to -= dir * 2.0f; // stand 2m in front
                
        // if (client.Network.CurrentSim.AvatarPositions.TryGetValue(e.SourceID, out pos))

        client.Self.AutoPilotLocal(
            (int)Math.Round(to.X), (int)Math.Round(to.Y), to.Z
        );
                
        client.Self.Movement.TurnToward(to);
    }

    private void ChatFromSimulator(object? sender, ChatEventArgs e)
    {
        if (e.AudibleLevel == ChatAudibleLevel.Fully && e.Type == ChatType.Normal)
        {
            Console.WriteLine(e.FromName + ": " + e.Message);

            var msgTrimmed = e.Message.Trim();
          
            if (msgTrimmed == "/comehere")
            {
                client.Self.Chat("yay im coming", 0, ChatType.Normal);
                WalkTowards(e.Position);
            }
            else if (msgTrimmed.StartsWith("/tts"))
            {
                var message = Regex.Replace(msgTrimmed, @"^/tts ?", "");
                pulseAudio.TextToSpeech("kathy", message);
            }
            else if (msgTrimmed.StartsWith("/play"))
            {
                var audioFilePath = Regex.Replace(msgTrimmed, @"^/play ?", "");
                pulseAudio.PlayWavAudioFile(audioFilePath);
            }
        }
    }
}