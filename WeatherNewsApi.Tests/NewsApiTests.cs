using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using WeatherNewsApi.Data;
using WeatherNewsApi.Services;

namespace WeatherNewsApi.Tests
{
    public class NewsApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly WebApplicationFactory<Program> _factory;

        public NewsApiTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Inject the missing configuration directly into the test memory
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:SecretKey"] = "a_very_long_secret_key_that_is_at_least_32_chars_long!!",
                        ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Use In-Memory instead of SQLite to avoid file path issues
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);
                    services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("TestDb"));
                });
            }).CreateClient();
        }

        [Fact]
        public async Task GetNews_ReturnsSuccessStatusCode()
        {
            var response = await _client.GetAsync("/news?secretId=123");

            // debugging
            //if (!response.IsSuccessStatusCode)
            //{
            //    var errorContent = await response.Content.ReadAsStringAsync();
            //    Assert.Fail($"API crashed with: {errorContent}");
            //}

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetNews_WithWrongSecretId_ReturnsForbidden()
        {
            var response = await _client.GetAsync("/news?secretId=999");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            var message = await response.Content.ReadAsStringAsync();
            Assert.Equal("Invalid secret id!", message);
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            var invalidLoginData = new { Username = "admin", Password = "123" };

            var response = await _client.PostAsJsonAsync("/login?secretId=123", invalidLoginData);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsToken()
        {
            var loginData = new { Username = "admin", Password = "password123" };

            var response = await _client.PostAsJsonAsync("/login?secretId=123", loginData);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            Assert.NotNull(result);
            Assert.True(result.ContainsKey("token"));
            Assert.NotNull(result["token"]);
        }

        [Fact]
        public async Task PostNews_WithValidToken_ReturnsCreated()
        {
            var loginData = new { Username = "admin", Password = "password123" };
            var loginResponse = await _client.PostAsJsonAsync("/login?secretId=123", loginData);
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var token = loginResult!["token"];

            var newPost = new { Title = "Testing integration", Content = "This content is an integration test" };
            var request = new HttpRequestMessage(HttpMethod.Post, "/news?secretId=123");
            request.Content = JsonContent.Create(newPost);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task GetNews_WhenServiceThrowsException_ReturnsCleanJsonError()
        {
            // setup the sabotaged factory
            var sabotagedFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // replace NewsService with a sabotaged version
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(NewsService));
                    if (descriptor != null) services.Remove(descriptor);

                    // create a mock that throws exception when GetAllAsync is called
                    var mockDb = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>()).Object;
                    var mockLogger = new Mock<ILogger<NewsService>>().Object;  
                    var mockService = new Mock<NewsService>(mockDb, mockLogger);
                    mockService.Setup(s => s.GetAllAsync()).ThrowsAsync(new Exception("Simulated service failure"));

                    // inject mock instead of real service
                    services.AddScoped(_ => mockService.Object);
                });
            });

            var client = sabotagedFactory.CreateClient();

            var response = await client.GetAsync("/news?secretId=123");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);


            var error = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(error);
            Assert.Equal(500, error!.Status);
            Assert.Equal("Internal Server Error. Please try again later.", error.Title);
        }
    }
}
