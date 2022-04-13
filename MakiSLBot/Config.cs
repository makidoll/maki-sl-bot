using System.Text.Json;

namespace MakiSLBot;

public class Config
{
    public static bool LoadedDotEnv = false;
    
    public static string? Get(string key)
    {
        if (LoadedDotEnv == false)
        {
            LoadedDotEnv = true;
            
            var dirNextToExe = Path.GetDirectoryName(
                (System.Reflection.Assembly.GetEntryAssembly() ?? typeof (Config).Assembly).Location
            );

            if (dirNextToExe == null)
            {
                throw new Exception("Can't get directory next to executable");
            }

            var envFilePath = Path.Combine(dirNextToExe, ".env");

            if (File.Exists(envFilePath))
            {
                foreach (var line in File.ReadAllLines(envFilePath))
                {
                    var parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2) continue;
                    Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                } 
            }
        }
        
        return Environment.GetEnvironmentVariable(key);
    }
}