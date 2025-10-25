using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Windows.Markup;
using MQTTnet;
using MQTTnet.Server;
using MQTTnet.Server; // already present, but needed for event args


public class StatefulMqqtServer
{
    private MqttServer _server;
    private readonly ConcurrentDictionary<string, ClientState> _clientStates;

    private static BrokerState _brokerState;

    static readonly object _locker = new object();


    public StatefulMqqtServer()
    {
        _clientStates = new ConcurrentDictionary<string, ClientState>();
        _brokerState = new BrokerState();
    }

    public async Task StartAsync(int port = 1883)
    {
        //Load existing state if exists
        _brokerState = BrokerState.Load("state.json");

        if (_brokerState != null)
        {

            Console.WriteLine("State is loading");
            //Restore client states from persisted broker state
            foreach (var kvp in _brokerState.Clients)
            {
                _clientStates[kvp.Key] = kvp.Value;
            }
        }
        //Create a MQTT client factory
        var factory = new MqttFactory();

        var options = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(port)
            .Build();

        _server = factory.CreateMqttServer(options);


        _server.ClientConnectedAsync += OnClientConnected;
        _server.ClientDisconnectedAsync += OnClientDisconnected;
        _server.InterceptingPublishAsync += OnMessageReceived;
        _server.ClientSubscribedTopicAsync += OnClientSubscribed;

        await _server.StartAsync();

        Console.WriteLine("╔═══════════════════════════════════════════╗");
        Console.WriteLine("║   MQTT STREAMING SERVER                   ║");
        Console.WriteLine("╚═══════════════════════════════════════════╝");
        Console.WriteLine($"[SERVER] Started on port {port}");
        Console.WriteLine("[SERVER] Waiting for client connections...\n");


    }
    private Task OnClientConnected(ClientConnectedEventArgs args)
    {
        var clientId = args.ClientId;

        if (_clientStates.TryGetValue(clientId, out var existingState))
        {
            existingState.ConnectedAt = DateTime.UtcNow;
            existingState.DisconnectedAt = null;

            _brokerState.Clients[clientId] = existingState;
            Console.WriteLine($"Client {clientId} reconnected ");

        }
        else
        {
            var state = new ClientState
            {
                ClientId = clientId,
                ConnectedAt = DateTime.UtcNow,
                MessagesSent = 0,
                MessagesReceived = 0,
                LastTemperatures = new List<double>(),
            };

            _clientStates[clientId] = state;
            _brokerState.Clients[clientId] = state;
            Console.WriteLine($"Client connected: {clientId} @ {state.ConnectedAt}");
        }

        lock (_locker)
        {
            _brokerState.Save();
        }
        DisplayAllStates();

        return Task.CompletedTask;
    }

    private Task OnClientDisconnected(ClientDisconnectedEventArgs args)
    {
        var clientId = args.ClientId;
        if (_clientStates.TryGetValue(clientId, out var state))
        {
            state.DisconnectedAt = DateTime.UtcNow;
            _brokerState.Clients[clientId] = state;
            _clientStates.TryRemove(clientId, out ClientState removed);
            Console.WriteLine($"Client disconnected: {clientId} @ {state.DisconnectedAt}");

        }

        lock (_locker)
        {
            _brokerState.Save();
        }

        DisplayAllStates();
        return Task.CompletedTask;
    }

    private Task OnClientSubscribed(ClientSubscribedTopicEventArgs args)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Client {args.ClientId} subscribed to: {args.TopicFilter.Topic}");
        return Task.CompletedTask;
    }

    private void DisplayAllStates()
    {
        Console.WriteLine($"\n--- Active Clients: {_clientStates.Count}---");
        foreach (var kvp in _clientStates)
        {
            var state = kvp.Value;
            //Console.WriteLine($"  @ {state.SessionId}: Sent={state.MessageSent}, Received={state.MessagesReceived}");
        }
    }

    private async Task OnMessageReceived(InterceptingPublishEventArgs args)
    {
        var clientId = args.ClientId;

        if (string.IsNullOrEmpty(clientId))
        {
            Console.WriteLine("Nothing Received from client");
            return;
        }

        if (!_clientStates.TryGetValue(clientId, out var state)) return;


        try
        {
            var payloadBytes = args.ApplicationMessage.PayloadSegment.ToArray();
            var payload = Encoding.UTF8.GetString(payloadBytes);
            
            var msg = JsonSerializer.Deserialize<StreamMessage>(payload);
            if (msg == null) return;

            state.MessagesReceived++;
            state.lastMessageReceivedAt = DateTime.UtcNow;

            double temp = msg.Temperature;
            if (!double.IsNaN(temp))
            {
                state.LastTemperatures.Add(temp);
                if (state.LastTemperatures.Count > 10)
                    state.LastTemperatures.RemoveAt(0);

                var deviationChecker = new CalculateDeviation();
                var alert = deviationChecker.GenerateAlert(state.LastTemperatures);

                if(alert != null)
                {
                    try
                    {
                        Console.WriteLine($"{clientId} : {alert.Message}");
                        await SendResponseToClient(clientId, alert);
                        state.MessagesSent++;
                        state.lastMessageSentAt = DateTime.UtcNow;
                    }
                    catch(Exception sendEx)
                    {
                        Console.WriteLine($"Failed to send Alert to client: {clientId}");
                    }
                }
                
            }

            _brokerState.Clients[clientId] = state;
            lock (_locker)
            {
                _brokerState.Save();
            }


        }
        catch (Exception ex)
        {
            Console.WriteLine($"failed to parse message from {clientId}: {ex.Message}");
        }
    }

    private async Task SendResponseToClient(string clientId, AlertMessage alert)
    {
        var responseTopic = $"response/{clientId}";
        var json = JsonSerializer.Serialize(
            alert,
            new JsonSerializerOptions
            {
                WriteIndented = false
            }
        );

        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic(responseTopic)
            .WithPayload(json)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _server.InjectApplicationMessage(new InjectedMqttApplicationMessage(mqttMessage)
        {
            SenderClientId = "Server"
        });

    }
    private void DisplayClientState(string sessionId)
    {
        if (_clientStates.TryGetValue(sessionId, out var state))
        {
            //Console.WriteLine($"   [STATE] Sent={state.MessageSent}, Received={state.MessagesReceived}, LastActivity={state.LastActivity:HH:mm:ss}\n");
        }
    }

    public async Task StopAsync() {
        await _server.StopAsync();
        Console.WriteLine("[SERVER] Stopped");
    }
}