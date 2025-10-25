public class StreamMessage
{
    public string SessionId { get; set; }
    public string MessageId { get; set; }
    public double Temperature { get; set; }
    public DateTime TimeStamp { get; set; }
    public int SequenceNumber { get; set; }
}