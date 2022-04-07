using System.Text.Json;

namespace MakiSLBot;

public class Config
{
    public string username { get; set; }
    public string password { get; set; }
    public string spawn { get; set; }
    public string slVoiceDir { get; set; }

    public string spawnSim;
    public int spawnX;
    public int spawnY;
    public int spawnZ;
    
    public static Config Get()
    {
        var jsonString = File.ReadAllText("config.json");
        var json = JsonSerializer.Deserialize<Config>(jsonString)!;

        var spawnSplit = json.spawn.Split(' ');
        if (spawnSplit.Length >= 4)
        {
            json.spawnSim = spawnSplit[0];
            json.spawnX = int.Parse(spawnSplit[1]);
            json.spawnY = int.Parse(spawnSplit[2]);
            json.spawnZ = int.Parse(spawnSplit[3]);
        }
        
        return json;
    }
}