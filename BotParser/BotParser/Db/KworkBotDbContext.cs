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
        public DbSet<FlCategory> FlCategories => Set<FlCategory>();
        public DbSet<SentFlOrder> SentFlOrders => Set<SentFlOrder>();
        public DbSet<YoudoCategory> YoudoCategories => Set<YoudoCategory>();
        public DbSet<SentYoudoOrder> SentYoudoOrders => Set<SentYoudoOrder>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<KworkCategory>()
                .HasOne(c => c.User)
                .WithMany(u => u.SelectedCategories)
                .HasForeignKey(c => c.UserId);

            modelBuilder.Entity<KworkCategory>()
                .HasIndex(c => new { c.UserId, c.CategoryId })
                .IsUnique();

            modelBuilder.Entity<SentOrder>()
                .HasIndex(s => new { s.UserTelegramId, s.ProjectId })
                .IsUnique();

            modelBuilder.Entity<FlCategory>()
                .HasOne(c => c.User)
                .WithMany() // Нет коллекции в User — просто связь
                .HasForeignKey(c => c.UserId);

            modelBuilder.Entity<FlCategory>()
                .HasIndex(c => new { c.UserId, c.CategoryId })
                .IsUnique();

            modelBuilder.Entity<SentFlOrder>()
                .HasIndex(s => new { s.UserTelegramId, s.ProjectId })
                .IsUnique();

            modelBuilder.Entity<YoudoCategory>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId);

            modelBuilder.Entity<YoudoCategory>()
                .HasIndex(c => new { c.UserId, c.CategoryId })
                .IsUnique();

            modelBuilder.Entity<SentYoudoOrder>()
                .HasIndex(s => new { s.UserTelegramId, s.TaskId })
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }
    }
}
