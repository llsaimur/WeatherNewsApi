using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WeatherNewsApi;
using WeatherNewsApi.Data;
using WeatherNewsApi.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
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
    builder.Services.AddSwaggerGen();
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddSqlite<AppDbContext>(connectionString);
    builder.Services.AddScoped<NewsService>();
    builder.Services.AddProblemDetails();

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

    //app.Use(async (context, next) =>
    //{
    //    string secretId = context.Request.Query["id"];
    //    if (secretId != "123")
    //    {
    //        context.Response.StatusCode = 403;

    //        await context.Response.WriteAsync("Invalid secret id!");
    //    }
    //    else
    //    {
    //        context.Response.Headers.Append("X-Brother-Status", "Learning");

    //        await next();
    //    }
    //});

    //app.MapGet("/hello", () => "Hello World!");

    app.UseAuthentication();
    app.UseAuthorization();



    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseDeveloperExceptionPage();
        app.UseStatusCodePages();
    }

    app.UseExceptionHandler();

    app.UseHttpsRedirection();

    //var summaries = new[]
    //{
    //    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    //};

    //app.MapGet("/weatherforecast", () =>
    //{
    //    var forecast =  Enumerable.Range(1, 5).Select(index =>
    //        new WeatherForecast
    //        (
    //            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
    //            Random.Shared.Next(-20, 55),
    //            summaries[Random.Shared.Next(summaries.Length)]
    //        ))
    //        .ToArray();
    //    return forecast;
    //})
    //.WithName("GetWeatherForecast")
    //.WithOpenApi();

    //var news = new List<NewsItem>
    //{
    //    new NewsItem { Id = 1, Title = "Modern .NET is Fast", Content = "...", PublishedAt = DateTime.Now }
    //};

    app.Use(async (context, next) =>
    {
        string secretId = context.Request.Query["secretId"];

        if (secretId != "123")
        {
            context.Response.StatusCode = 403;

            await context.Response.WriteAsync("Invalid secret id!");
        }
        else
        {
            await next();
        }

    });

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

    app.MapGet("/news", async (NewsService service) => await service.GetAllAsync());

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

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public record LoginRequest(string Username, string Password);
