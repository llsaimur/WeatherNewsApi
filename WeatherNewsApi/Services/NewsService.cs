using Microsoft.EntityFrameworkCore;
using WeatherNewsApi.Data;

namespace WeatherNewsApi.Services
{
    public class NewsService
    {
        //private List<NewsItem> _newsItems = new();
        private readonly AppDbContext _context;
        private readonly ILogger<NewsService> _logger;

        public NewsService(AppDbContext context, ILogger<NewsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public virtual async Task<List<NewsResponse>> GetAllAsync() => await _context.NewsItems
            .Select(item => new NewsResponse(item.Id, item.Title, item.Content, item.PublishedAt))
            .ToListAsync();

        public async Task AddAsync(NewsItem item)
        {
            if (item == null) 
            {
                _logger.LogError("Attempted to add a null item.");
                throw new ArgumentNullException(nameof(item), "News item cannot be null");
            }

            await _context.NewsItems.AddAsync(item);
            await _context.SaveChangesAsync();
        }

        public async Task<NewsResponse?> GetAsync(int id) => await _context.NewsItems
            .Where(item => item.Id == id)
            .Select(item => new NewsResponse(item.Id, item.Title, item.Content, item.PublishedAt))
            .FirstOrDefaultAsync();

        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.NewsItems.FindAsync(id);

            if (item == null)
            {
                _logger.LogWarning("Delete failed: News item with ID {Id} was not found.", id);
                return false;
            }

            _context.NewsItems.Remove(item);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted news item with ID: {Id}", id);

            return true;
        }

        public async Task<bool> UpdateAsync(int id, CreateNewsRequest request)
        {
            var item = await _context.NewsItems.FindAsync(id);

            if (item is null)
            {
                return false;
            }

            item.Title = request.Title;
            item.Content = request.Content;

            await _context.SaveChangesAsync();

            return true;
        }
    }

    public record CreateNewsRequest(string Title, string Content);
    public record NewsResponse(int id, string Title, string Content, DateTime PublishedAt);
}
