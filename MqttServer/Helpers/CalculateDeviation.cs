public class CalculateDeviation
{
    
    public AlertMessage GenerateAlert(List<double> temps)
    {
        if (temps == null || temps.Count < 10)
        {
            return null;
        }

        double checkTemp = temps.Last();
        double avg = temps.Take(temps.Count - 1).Average();
        double deviation = Math.Abs(checkTemp - avg);

        if (deviation > 5)
        {
            return new AlertMessage
            {
                deviation = deviation,
                Message = $"Alert Spike Detected::::Avg={avg:F2}, Deviation={deviation:F2}",
            };
        }

        return null;
    }


}