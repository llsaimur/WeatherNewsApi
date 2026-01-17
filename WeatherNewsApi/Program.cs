using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WeatherNewsApi;
using WeatherNewsApi.Data;
using WeatherNewsApi.Models.DTOs;
using WeatherNewsApi.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) // hides "noise" from .NET logs
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/news-api-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting the news API...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();


    // Add services to the container.
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        options.IncludeXmlComments(xmlPath);
    });
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddSqlite<AppDbContext>(connectionString);
    builder.Services.AddScoped<NewsService>();
    builder.Services.AddProblemDetails();
    builder.Services.AddScoped<ExternalWeatherService>();
    builder.Services.AddHealthChecks();

    builder.Services.AddHttpClient("OpenWeather", client =>
    {
        client.BaseAddress = new Uri("https://api.openweathermap.org/data/2.5/");
    });

    var secretKey = builder.Configuration["Authentication:SecretKey"];

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    });

    var app = builder.Build();

    // 1. safety net
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

        // to help Serilog map the "Path" and "Method" properties
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        };
    });

    // 2. logging and diagnostics
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        // shows detailed erros only for you
        app.UseDeveloperExceptionPage();
    }

    app.UseStatusCodePages(); // turns 404s/401s int problem details responses

    // 3. security and routing
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();

    app.Use(async (context, next) =>
    {
        string secretId = context.Request.Query["secretId"];

        using (Serilog.Context.LogContext.PushProperty("ClientSecretId", secretId))
        {

            if (secretId != "123")
            {
                Log.Warning("Request with invalid secret id: {SecretId}", secretId);
                context.Response.StatusCode = 403;

                await context.Response.WriteAsync("Invalid secret id!");

                return;
            }

            await next();
        }

    });

    app.MapHealthChecks("/health");

    app.MapPost("/login", (LoginRequest user) =>
    {
        if (user.Username == "admin" && user.Password == "password123")
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, "Admin")
            };

            var token = new JwtSecurityToken(claims: claims, expires: DateTime.Now.AddHours(1), signingCredentials: creds);

            return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }

        if (user.Username == "editor" && user.Password == "password123")
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, "Editor")
            };

            var token = new JwtSecurityToken(claims: claims, expires: DateTime.Now.AddHours(1), signingCredentials: creds);

            return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }

        return Results.Unauthorized();
    });

    /// <summary>
    /// Retrieves the latest news items along with live weather data for London.
    /// </summary>
    /// <remarks>
    /// This endpoint calls the OpenWeather API in parallel with the database query
    /// to provide a fast, combined response.
    /// </remarks>
    /// <returns>
    /// A combined object containing news items and weather summary.
    /// </returns>
    app.MapGet("/news", async (NewsService newsService, ExternalWeatherService weatherService) => 
    {
        var newsTask = newsService.GetAllAsync();

        var weatherTask = weatherService.GetLiveWeatherAsync("London");

        await Task.WhenAll(newsTask, weatherTask);

        var news = await newsTask;
        var weather = await weatherTask;

        string summary = weather != null
            ? $"It's {weather.Weather[0].Description} in {weather.Name} with a temperature of {weather.Main.Temp}K."
            : "Weather data is unavailable.";

        return Results.Ok(new NewsWeatherResponse
        {
            News = news,
            WeatherSummary = summary,
            Temperature = weather?.Main.Temp ?? 0
        });
    });

    app.MapPost("/news", async (CreateNewsRequest request, NewsService service) =>
    {
        if (request.Title.Length < 10 || request.Content.Length < 10)
        {
            return Results.BadRequest("Invalid news data");
        }

        var newsItem = new NewsItem() { Title = request.Title, Content = request.Content, PublishedAt = DateTime.Now };

        await service.AddAsync(newsItem);

        return Results.Created($"/news/{newsItem}", newsItem);
    }).RequireAuthorization();

    app.MapGet("/news/{Id}", async (int id, NewsService service) =>
    {
        var item = await service.GetAsync(id);

        if (item == null)
        {
            return Results.NotFound();
        }
        else
        {
            return Results.Ok(item);
        }
    });

    app.MapPut("/news/{id}", async (int id, CreateNewsRequest request, NewsService service) =>
    {
        if (request.Title.Length < 10 || request.Content.Length < 10)
        {
            return Results.BadRequest("Invalid news data");
        }

        var result = await service.UpdateAsync(id, request);

        if (!result)
        {
            return Results.NotFound();
        }

        return Results.NoContent();

    }).RequireAuthorization();

    app.MapDelete("/news/{Id}", async (int id, NewsService service, ClaimsPrincipal user) =>
    {
        var userName = user.Identity?.Name ?? "Anonymous";

        Log.Information("User {User} is requesting a deletion of {Id}", userName, id);

        if (await service.DeleteAsync(id))
        {
            return Results.NoContent();
        }

        return Results.NotFound();
    }).RequireAuthorization("AdminOnly");


    app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "The application failed to start correctly.");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public record LoginRequest(string Username, string Password);
