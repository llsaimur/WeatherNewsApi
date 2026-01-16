using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using WeatherNewsApi.Models.DTOs;
using WeatherNewsApi.Services;

namespace WeatherNewsApi.Tests
{
    public class ExternalWeatherServiceTests
    {
        [Fact]
        public async Task GetLiveWeather_WhenApiReturnsData_ReturnsMappedObject()
        {
            // Arrange
            var handlerMock = new Mock<HttpMessageHandler>();

            var fakeResponse = new OpenWeatherResponse
            {
                Name = "TestCity",
                Main = new MainData { Temp = 300.15, Humidity = 80 },
            };

            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage
               {
                   StatusCode = System.Net.HttpStatusCode.OK,
                   Content = JsonContent.Create(fakeResponse),
               });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://api.openweathermap.org/data/2.5/")
            };

            var mockFactory = new Mock<IHttpClientFactory>();
            mockFactory.Setup(_ => _.CreateClient("OpenWeather")).Returns(httpClient);

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["WeatherApi:ApiKey"]).Returns("fake-key");


            var service = new ExternalWeatherService(mockFactory.Object, mockConfig.Object);

            // Act
            var result = await service.GetLiveWeatherAsync("TestCity");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestCity", result.Name);
            Assert.Equal(300.15, result.Main.Temp);
        }
    }
}
