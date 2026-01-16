using WeatherNewsApi.Services;

namespace WeatherNewsApi.Models.DTOs
{
    public class NewsWeatherResponse
    {
        public List<NewsResponse> News { get; set; } = new();
        public string WeatherSummary { get; set; } = string.Empty;
        public double Temperature { get; set; }
    }
}
