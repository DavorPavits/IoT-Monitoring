public class ClientState
{
    public string ClientId { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime? DisconnectedAt { get; set; }
    public int MessagesSent { get; set; }
    public int MessagesReceived { get; set; }
    public DateTime? lastMessageSentAt { get; set; }
    public DateTime? lastMessageReceivedAt { get; set; }
    public List<double> LastTemperatures { get; set; } = new();
    
}