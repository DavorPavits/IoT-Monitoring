public class ClientState
{
    public string ClientId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime DisconnectedAt { get; set; }
    public int MessagesSent { get; set; }
    public DateTime lastMessageSent { get; set; }
    public int MessagesReceived { get; set; }
    public DateTime lastMessageReceived { get; set; }
    
}