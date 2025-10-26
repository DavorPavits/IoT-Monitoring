using System.Text.Json;

public class ReconnectLogger
{
    public Dictionary<string, ReconnectLog> Logs { get; set; } = new();
    private const string LogFilePath = "./data/reconnect_logs.json";
    public void SaveLog(ReconnectLog log)
    {

        try
        {
            var directory = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var existing = Load();
            existing.Logs[log.ClientId] = log;

            var json = JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(LogFilePath, json);

            Console.WriteLine($"[Logs] Saved to {LogFilePath} - {Logs.Count} clients");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read existing log file: {ex.Message}");
        }
    }

    public static ReconnectLogger Load(string path = null)
    {
        path ??= LogFilePath;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var logs = JsonSerializer.Deserialize<ReconnectLogger>(json);

                if (logs != null && logs.Logs.Count > 0)
                {
                    Console.WriteLine($"Logs loaded from {path} - logs for clients :{logs.Logs.Count}");

                    foreach (var kvp in logs.Logs)
                    {
                        Console.WriteLine($"{kvp}: downtime {kvp.Value.DowntimeSeconds}");
                    }
                    return logs;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load logs: {ex.Message}");
        }

        Console.WriteLine("No existing logs file found");
        return new ReconnectLogger();
    }
}