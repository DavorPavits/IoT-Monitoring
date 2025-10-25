using System.Collections;
using System.Collections.Concurrent;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Windows.Markup;
using MQTTnet;
using MQTTnet.Server;
using MQTTnet.Server; // already present, but needed for event args



class Program {
    static async Task Main(string[] args)
    {
        var port = 1883;

        if (args.Length > 0 && int.TryParse(args[0], out var custonPort))
        {
            port = custonPort;
        }

        var server = new StatefulMqqtServer();
        await server.StartAsync(port);

        // Console.WriteLine("Press 'Q' to stop the server...\n");

        // while (true)
        // {
        //     var key = Console.ReadKey(true);
        //     if (key.Key == ConsoleKey.Q) break;
        // }

        // await server.StopAsync();
        // Console.WriteLine("Server shutdown complete.");
        await Task.Delay(Timeout.Infinite);
    }
}
