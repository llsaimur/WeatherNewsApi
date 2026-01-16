using WeatherNewsApi.Models.DTOs;

namespace WeatherNewsApi.Services
{
    public class ExternalWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ExternalWeatherService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient("OpenWeather");
            _configuration = configuration;
        }

        public async Task<OpenWeatherResponse?> GetLiveWeatherAsync(string city)
        {
            var apiKey = _configuration["WeatherApi:ApiKey"];

            var response = await _httpClient.GetAsync($"weather?q={city}&appid={apiKey}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            
            return await response.Content.ReadFromJsonAsync<OpenWeatherResponse>();
        }
    }
}
