public class MultiClientManager
{
    private readonly List<StatefulMqttClient> _clients;
    private readonly string _broker;
    private readonly int _port;


    public MultiClientManager(string broker = "localhost", int port = 1883)
    {
        _clients = new List<StatefulMqttClient>();
        _broker = broker;
        _port = port;
    }

    public async Task CreateAndConnectClients(int clientCount)
    {
        Console.WriteLine($"---Creating {clientCount} clients---\n");

        var connectTasks = new List<Task>();

        for (int i = 0; i < clientCount; i++)
        {
            var clientId = $"Client-{i + 1:D3}";
            var client = new StatefulMqttClient(clientId);
            _clients.Add(client);

            //Connect each client
            connectTasks.Add(client.ConnectAsync(_broker, _port));

            //Add delay 
            await Task.Delay(100);
        }

        //Wait for all clients to connect
        await Task.WhenAll(connectTasks);
        Console.WriteLine($"All {clientCount} clients connected!!!");
    }


    public async Task StartAllClientsStreaming(string topic, int intervalMs, int messageCount)
    {
        Console.WriteLine($"===Starting streaming from {_clients.Count} clients");

        var cts = new CancellationTokenSource();
        var streamingTasks = new List<Task>();

        //Streaming for each client in parallel
        foreach (var client in _clients)
        {
            var task = client.StartStreamingAsync(topic, intervalMs, messageCount, cts.Token);
            streamingTasks.Add(task);
        }

        //Wait for all clients
        await Task.WhenAll(streamingTasks);

        Console.WriteLine($"All clients completed streaming\n");
    }

    public void DisplayAllStats()
    {
        Console.WriteLine($"\n=== Client Statistics===");
        foreach (var client in _clients)
        {
            client.DisplayStats();
        }
        Console.WriteLine();
    }

    public async Task DisconnectAllClients()
    {
        Console.WriteLine("\n===Disconnecting all clients===");

        var disconnectTasks = new List<Task>();
        foreach (var client in _clients) {
            disconnectTasks.Add(client.DisconnectAsync());
        }

        await Task.WhenAll(disconnectTasks);
        Console.WriteLine("All clients disconnected\n");

    }
}