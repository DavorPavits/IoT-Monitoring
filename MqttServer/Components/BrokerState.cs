using System.Text.Json;

public class BrokerState
{
    public Dictionary<string, ClientState> Clients { get; set; } = new();

    private const string StateFilePath = "./data/state.json";
    

    public static BrokerState Load(string path = null)
    {
        path ??= StateFilePath;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var state = JsonSerializer.Deserialize<BrokerState>(json);
                
                if(state != null && state.Clients.Count > 0)
                {
                    Console.WriteLine($"State loaded from {path} - {state.Clients.Count} clients");

                    foreach (var kvp in state.Clients) 
                    {
                        Console.WriteLine($"{kvp.Key}: Sent={kvp.Value.MessagesSent}, Received={kvp.Value.MessagesReceived}");

                    }
                    return state;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load state: {ex.Message}");
        }

        Console.WriteLine("No existing state found, starting new...");
        return new BrokerState();

        
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(StateFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StateFilePath, json);
            Console.WriteLine($"[STATE] Saved to {StateFilePath} - {Clients.Count} clients");
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Failed to save state: {ex.Message}");
        }
    }
}