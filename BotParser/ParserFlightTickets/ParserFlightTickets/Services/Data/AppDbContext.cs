using Microsoft.EntityFrameworkCore;
using ParserFlightTickets.Models;


namespace ParserFlightTickets.Services.Data
{
    public class AppDbContext : DbContext
    {
        // Конструктор без параметров — для миграций и тестов
        public AppDbContext()
        {
        }

        // Главный конструктор — именно он будет использоваться DI
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Setting> Settings { get; set; }
        public DbSet<PublishedDeal> PublishedDeals { get; set; }

        // OnConfiguring — можно оставить пустым или убрать, если используешь AddDbContext
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // НЕ нужно указывать UseSqlite здесь, если используешь AddDbContext
            // optionsBuilder.UseSqlite("Data Source=bot2.db");
        }
    }
}
