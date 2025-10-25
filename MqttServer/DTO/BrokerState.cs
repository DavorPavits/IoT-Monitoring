using System.Text.Json;

public class BrokerState
{
    public Dictionary<string, ClientState> Clients { get; set; } = new();
    //public Dictionary<string, double> LastTemperatures { get; set; } = new();

    private const string StateFilePath = "state.json";
    public static BrokerState Load(string path = null)
    {
        path ??= StateFilePath;
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var state = JsonSerializer.Deserialize<BrokerState>(json);
                return state ?? new BrokerState();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load state: {ex.Message}");
        }

        Console.WriteLine("No existing state found, starting new...");
        return new BrokerState();

        
    }

    public void Save(string path = "state.json")
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            Console.WriteLine($"[STATE] Savor to {StateFilePath}");
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Failed to save state: {ex.Message}");
        }
    }
}