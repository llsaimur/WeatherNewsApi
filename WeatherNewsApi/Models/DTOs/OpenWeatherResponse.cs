namespace WeatherNewsApi.Models.DTOs
{
    public class OpenWeatherResponse
    {
        public MainData Main { get; set; } = new MainData();
        public WeatherDescription[] Weather { get; set; } = Array.Empty<WeatherDescription>();
        public string Name { get; set; } = string.Empty;
    }

    public class MainData
    {
        public double Temp { get; set; }
        public int Humidity { get; set; }
    }

    public class WeatherDescription
    {
        public string Description { get; set; } = string.Empty;
    }
}
