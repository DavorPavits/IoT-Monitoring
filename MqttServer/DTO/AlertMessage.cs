public class AlertMessage
{
    public double deviation { get; set; }
    public string Message { get; set; } = string.Empty;

    public override string ToString()
    {
        return Message;
    }

    
};