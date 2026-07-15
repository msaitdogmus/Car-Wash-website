namespace DryCar.Models;

public class WeatherInfo
{
    public string City { get; set; } = "Kırşehir";

    public double TemperatureC { get; set; }

    public string Summary { get; set; } = "";

    public int Humidity { get; set; }

    public double WindKmh { get; set; }

    public double FeelsLikeC { get; set; }
}
