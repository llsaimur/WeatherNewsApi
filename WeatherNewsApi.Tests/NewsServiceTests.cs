using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WeatherNewsApi.Data;
using WeatherNewsApi.Services;

namespace WeatherNewsApi.Tests
{
    public class NewsServiceTests
    {
        private AppDbContext GetDatabase()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var databaseContext = new AppDbContext(options);

            databaseContext.Database.EnsureCreated();

            return databaseContext;
        }

        [Fact]
        public async Task DeleteAsync_ReturnsFalse_WhenItemDoesNotExist()
        {
            var db = GetDatabase();
            var service = new NewsService(db, NullLogger<NewsService>.Instance);

            var result = await service.DeleteAsync(999);

            Assert.False(result);
        }

        [Fact]
        public async Task AddAsync_ShouldSaveNewsItemToDatabase()
        {
            var db = GetDatabase();
            var service = new NewsService(db, NullLogger<NewsService>.Instance);
            var item = new NewsItem { Title = "Test", Content = "This is a test" };

            await service.AddAsync(item);

            var items = await db.NewsItems.ToListAsync();
            Assert.Single(items);
            Assert.Equal("Test", items[0].Title);
            Assert.Equal("This is a test", items[0].Content);
        }

        [Fact]
        public async Task AddAsync_ShouldIgnoreNewsItemToDatabase_WhenNewsItemIsNull()
        {
            var db = GetDatabase();
            var service = new NewsService(db, NullLogger<NewsService>.Instance);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.AddAsync(null!));
        }
    }
}
