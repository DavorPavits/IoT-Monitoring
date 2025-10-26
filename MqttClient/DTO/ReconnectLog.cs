public class ReconnectLog
{
    public string ClientId { get; set; }
    public DateTime DisconnectedAt { get; set; }
    public DateTime RecconectedAt { get; set; }
    public double DowntimeSeconds { get; set; }
    public int Attempts { get; set; }
}