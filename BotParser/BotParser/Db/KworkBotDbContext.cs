using Microsoft.EntityFrameworkCore;
using BotParser.Models;

namespace BotParser.Db
{
    public class KworkBotDbContext : DbContext
    {
        public KworkBotDbContext(DbContextOptions<KworkBotDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<KworkCategory> KworkCategories => Set<KworkCategory>();
        public DbSet<SentOrder> SentOrders => Set<SentOrder>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<KworkCategory>()
                .HasOne(c => c.User)
                .WithMany(u => u.SelectedCategories)
                .HasForeignKey(c => c.UserId);

            base.OnModelCreating(modelBuilder);
        }
    }
}
