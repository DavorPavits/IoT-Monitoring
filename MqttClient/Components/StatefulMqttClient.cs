using MQTTnet;
using MQTTnet.Client;
using System;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


public class StatefulMqttClient
{
    private IMqttClient _client;
    private readonly string _clientId;
    private ClientState _localState;
    private int _sequenceNumber;
    private CancellationTokenSource _streamnCts;
    private string _borker;

    static readonly object _locker = new object();


    private readonly int[] _port = { 1883, 1884 };
    private int _currentPortIndex = 0;
    private bool _isReconnecting = false;


    public string ClientId => _clientId;
    public ClientState State => _localState;

    private static ReconnectLogger _logger;
    public StatefulMqttClient(string clientId)
    {
        _clientId = clientId;
        _localState = new ClientState
        {
            SessionId = clientId,
            ConnectedAt = DateTime.UtcNow
        };

        _sequenceNumber = 0;
        _logger = new ReconnectLogger();
    }

    public async Task ConnectAsync(String broker = "localhost", int port = 1883)
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(broker, port)
            .WithClientId(_clientId)
            .WithCleanSession(false)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
            .Build();

        _client.ApplicationMessageReceivedAsync += OnMessageReceived;
        _client.DisconnectedAsync += OnDisconnected;
        _client.ConnectedAsync += OnConnected;

        try
        {
            await _client.ConnectAsync(options);
            _localState.ConnectedAt = DateTime.UtcNow;
            await _client.SubscribeAsync($"response/{_clientId}");

            StartRecconectHandler(broker);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{_clientId} Initial Connection failed: {ex.Message}");
            throw;
        }
    }

    private Task OnConnected(MqttClientConnectedEventArgs args)
    {
        Console.WriteLine($"\n✅ [{_clientId}] CONNECTED successfully!");
        //TODO: Save the local state in ordeR to track what has been sent

        Console.WriteLine($"   Previous stats - Sent: {_localState.MessagesSent}, Received: {_localState.MessagesReceived}"); // Not Working ????
        
        _isReconnecting = false;
        return Task.CompletedTask;
    }

    private async Task StartRecconectHandler(string broker)
    {
        // Background reconnect loop
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    if (!_client.IsConnected && !_isReconnecting)
                    {
                        _isReconnecting = true;
                        await AttemptRecconectAsync(broker);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{_clientId}] Reconnect failed: {ex.Message}");
                }
            }
        });
    }
    
    private async Task AttemptRecconectAsync(string broker)
    {
        Console.WriteLine($"{_clientId} Starting recconection attemps...");
        int attemp = 0;

        DateTime recconectStart = DateTime.UtcNow;
        DateTime? disconnectedAt = _localState.DisconnectedAt;

        
        _logger = ReconnectLogger.Load();

        while (!_client.IsConnected)
        {
            attemp++;
            int currPort = _port[_currentPortIndex];

            try
            {
                Console.WriteLine($"Attemp {attemp} on port {currPort}");

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(broker, currPort)
                    .WithClientId(_clientId)
                    .WithCleanSession(false)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                    .Build();

                await _client.ConnectAsync(options, CancellationToken.None);

                if (_client.IsConnected)
                {
                    DateTime recconectedAt = DateTime.UtcNow;
                    _localState.ConnectedAt = DateTime.UtcNow;
                    await _client.SubscribeAsync($"response/{_clientId}");

                    double downtime = disconnectedAt.HasValue
                        ? (recconectedAt - disconnectedAt.Value).TotalSeconds
                        : (recconectedAt - recconectStart).TotalSeconds;

                    Console.WriteLine($"{_clientId} Recconected to port {currPort} after {downtime:F2} s");

                    //Save log
                    var log = new ReconnectLog
                    {
                        ClientId = _clientId,
                        DisconnectedAt = disconnectedAt ?? recconectStart,
                        RecconectedAt = recconectedAt,
                        DowntimeSeconds = downtime,
                        Attempts = attemp
                    };

                    lock (_locker)
                    {
                        _logger.SaveLog(log);
                    }

                    _isReconnecting = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed on port {currPort}: {ex.Message}");

                if (attemp % 3 == 0)
                {
                    _currentPortIndex = (_currentPortIndex + 1) % _port.Length;
                    Console.WriteLine($"Switching to port {_port[_currentPortIndex]}");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        _localState.MessagesReceived++;
        //_localState.LastActivity = DateTime.UtcNow;

        var payload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
        var alertMessage = JsonSerializer.Deserialize<AlertMessage>(payload);

        Console.WriteLine($"\n In {ClientId} => {alertMessage.Message}");
        //DisplayLocalState();

        return Task.CompletedTask;
    }

    private Task OnDisconnected(MqttClientDisconnectedEventArgs args)
    {
        Console.WriteLine($"\n[CLIENT] {_clientId} Disconnected: {args.Reason}");

        //Update the disconected state
        _localState.DisconnectedAt = DateTime.UtcNow;

        if (args.Exception != null)
        {
            Console.WriteLine($"[CLIENT] Exception: {args.Exception.Message}");
        }
    
        if (!_isReconnecting)
        {
            Console.WriteLine($"Will attempt reconnection...");
        }

        return Task.CompletedTask;
    }

    public async Task StartStreamingAsync(string topic, int intervalMs, int messageCount, CancellationToken ct)
    {
        Random rand = new Random();
        var temp = 100.0;

        try
        {
            for (int i = 0; i < messageCount && !ct.IsCancellationRequested; i++)
            {
                //Check if client is connected
                if (!_client.IsConnected)
                {
                    Console.WriteLine($"{ClientId} Not connected!");
                    await Task.Delay(1000, ct);
                    continue;
                }
                var spikes = rand.NextDouble() < 0.95 ? (rand.NextDouble() - 0.5) * 10 : (rand.NextDouble() - 0.5) * 20;
                var currTemp = temp + spikes;

                var message = new StreamMessage
                {
                    SessionId = _clientId,
                    MessageId = Guid.NewGuid().ToString(),
                    Temperature = currTemp,
                    TimeStamp = DateTime.UtcNow,
                    SequenceNumber = ++_sequenceNumber
                };

                await PublishMessageAsync(topic, message);
                await Task.Delay(intervalMs, ct);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"\n{_clientId} streaming cancelled");
        }

        Console.WriteLine($"[STREAM] Total Messages sent: {_localState.MessagesSent}\n");
    }



    private async Task PublishMessageAsync(string topic, StreamMessage message)
    {
        try
        {
            if (!_client.IsConnected)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Skipping Seq={message.SequenceNumber} - Not connected");
                return;
            }

            var json = JsonSerializer.Serialize(message);
            var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(json)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

            Console.WriteLine($"Sent: Seq={message.SequenceNumber}, Temperature='{message.Temperature}'");
        
            await _client.PublishAsync(mqttMessage, CancellationToken.None);
            _localState.MessagesSent++;
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Failed to publish {message.SequenceNumber}");
        }
        

        //DisplayLocalState();
    }

    private void DisplayLocalState()
    {
        var duration = DateTime.UtcNow - _localState.ConnectedAt;
        Console.WriteLine($"    [STATE] Sent={_localState.MessagesSent}, Received={_localState.MessagesReceived}, Duration={duration.TotalSeconds:F1}s");
    }

    public async Task DisconnectAsync()
    {
        StopStreaming();
        DisplayStats();
        await _client.DisconnectAsync();
        Console.WriteLine($"\n[Client] Disconnected from broker");
    }

    public void StopStreaming()
    {
        _streamnCts?.Cancel();
    }

    public void DisplayStats() {
        var duration = DateTime.UtcNow - _localState.ConnectedAt;
        Console.WriteLine($"{_clientId}: Sent={_localState.MessagesSent}, Received={_localState.MessagesReceived}, Duration={duration.TotalSeconds:F1}s");

    }
}