using Microsoft.EntityFrameworkCore;

namespace WeatherNewsApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    }
}
