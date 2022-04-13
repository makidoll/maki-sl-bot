#define DEBUG

using System.Drawing.Imaging;
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
        voiceGateway.MicLevel = 45;

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
        client.Self.IM += IM;
        
        client.Network.EventQueueRunning += ClientOnEventQueueRunning;

        var firstName = Config.Get("FIRST_NAME");
        var lastName = Config.Get("LAST_NAME");
        var password = Config.Get("PASSWORD");
        const string channel = "ItsDotNetAlright";
        const string version = "maki.cafe";
        
        var spawn = (Config.Get("SPAWN") ?? "").Split(' ');
        var startLocation = spawn.Length >= 4
            ? NetworkManager.StartLocation(spawn[0], int.Parse(spawn[1]), int.Parse(spawn[2]), int.Parse(spawn[3]))
            : null;

        var loginOk = startLocation != null
            ? client.Network.Login(firstName, lastName, password, channel, startLocation, version)
            : client.Network.Login(firstName, lastName, password, channel, version);

        if (!loginOk) {
            throw new Exception("Failed to login: " + client.Network.LoginMessage);
        }
        
        Console.WriteLine("Logged in: " + client.Network.LoginMessage);

        Console.WriteLine("Starting voice gateway");

        var slVoiceDir = Config.Get("SL_VOICE_DIR");
        if (slVoiceDir == null)
        {
            throw new Exception("SL_VOICE_DIR not provided");
        }
        
        pulseAudio = new PulseAudio(slVoiceDir);
    
        voiceGateway = new VoiceGateway(client);
        voiceGateway.OnSessionCreate += VoiceGatewayOnSessionCreate;
        voiceGateway.OnSessionRemove += VoiceGatewayOnSessionRemove;
        voiceGateway.OnAuxGetCaptureDevicesResponse += VoiceGatewayOnAuxGetCaptureDevicesResponse;
        voiceGateway.OnAuxGetRenderDevicesResponse += VoiceGatewayOnAuxGetRenderDevicesResponse;
        voiceGateway.Start();
    }

    private void IM(object? sender, InstantMessageEventArgs e)
    {
        if (e.IM.FromAgentName != Config.Get("OWNER_NAME")) return;
        if (e.IM.Dialog == InstantMessageDialog.RequestTeleport)
        {
            client.Self.TeleportLureRespond(e.IM.FromAgentID, e.IM.IMSessionID, true);
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

    private async void ChatFromSimulator(object? sender, ChatEventArgs e)
    {
        if (e.AudibleLevel == ChatAudibleLevel.Fully && e.Type == ChatType.Normal)
        {
            Console.WriteLine(e.FromName + ": " + e.Message);

            var msgTrimmed = e.Message.Trim();
          
            if (msgTrimmed.StartsWith("/tts"))
            {
                var message = Regex.Replace(msgTrimmed, @"^/tts ?", "");
                pulseAudio.TextToSpeech("kathy", message);
            }
            else if (msgTrimmed.StartsWith("/play"))
            {
                try
                {
                    var audioFilePath = Regex.Replace(msgTrimmed, @"^/play ?", "");
                    var bytes = await File.ReadAllBytesAsync(audioFilePath);
                    await pulseAudio.PlayAudioFile(bytes);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }
            }
        }
    }
}