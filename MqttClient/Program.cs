using System.Text.Json;

class Program
{    
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════╗");
        Console.WriteLine("║   MQTT MULTI-CLIENT TEST                  ║");
        Console.WriteLine("╚═══════════════════════════════════════════╝\n");


        string broker = "localhost";
        int port = 1883;
        int clientsCount = 5;
        int messagesPerClient = 10;
        int intervalMs = 1000;

        //Parse Arguments
        if (args.Length >= 1 && int.TryParse(args[0], out var customClientCount))
        {
            clientsCount = customClientCount;
        }

        if (args.Length >= 2 && int.TryParse(args[1], out var customMessages))
        {
            messagesPerClient = customMessages;
        }

        if (args.Length >= 3 && int.TryParse(args[2], out var customInterval))
        {
            intervalMs = customInterval;

        }

        if (args.Length >= 4) broker = args[3];

        Console.WriteLine($"Configuration:");
        Console.WriteLine($"  Broker: {broker}:{port}");
        Console.WriteLine($"  Clients: {clientsCount}");
        Console.WriteLine($"  Messages per client: {messagesPerClient}");
        Console.WriteLine($"  Interval: {intervalMs}ms\n");

        var manager = new MultiClientManager(broker, port);


        try
        {
            //Create and connect all clients
            await manager.CreateAndConnectClients(clientsCount);
            await Task.Delay(1000);

            //Start streaming 
            await manager.StartAllClientsStreaming("data/stream", intervalMs, messagesPerClient);

            await Task.Delay(2000);

            manager.DisplayAllStats();

            //Disconnect
            await manager.DisconnectAllClients();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

}