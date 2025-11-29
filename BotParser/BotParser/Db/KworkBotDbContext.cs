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
        public DbSet<FrCategory> FrCategories => Set<FrCategory>();
        public DbSet<SentFrOrder> SentFrOrders => Set<SentFrOrder>();
        public DbSet<SentWsOrder> SentWsOrders => Set<SentWsOrder>();
        public DbSet<WorkspaceCategory> WorkspaceCategories => Set<WorkspaceCategory>();
        public DbSet<UserKeywordFilter> UserKeywordFilters => Set<UserKeywordFilter>();
        public DbSet<AllParsedOrder> AllParsedOrders => Set<AllParsedOrder>();
        public DbSet<ProfiCategory> ProfiCategories => Set<ProfiCategory>();
        public DbSet<SentProfiOrder> SentProfiOrders => Set<SentProfiOrder>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // === User ===
            modelBuilder.Entity<User>(e =>
            {
                e.HasKey(u => u.Id);
                e.Property(u => u.Username).HasMaxLength(100);
            });

            // === KworkCategory ===
            modelBuilder.Entity<KworkCategory>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.UserId, x.CategoryId }).IsUnique();
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // === FlCategory ===
            modelBuilder.Entity<FlCategory>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.UserId, x.CategoryId }).IsUnique();
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // === YoudoCategory ===
            modelBuilder.Entity<YoudoCategory>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.UserId, x.CategoryId }).IsUnique();
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // === FrCategory — САМОЕ ГЛАВНОЕ ===
            modelBuilder.Entity<FrCategory>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.UserId, x.CategoryId }).IsUnique();
                e.Property(x => x.Name).HasMaxLength(200);
                e.Property(x => x.NotificationInterval).HasMaxLength(20).HasDefaultValue("off");
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // === Sent-таблицы ===
            modelBuilder.Entity<SentOrder>(e => e.HasIndex(x => new { x.UserTelegramId, x.ProjectId }).IsUnique());
            modelBuilder.Entity<SentFlOrder>(e => e.HasIndex(x => new { x.UserTelegramId, x.ProjectId }).IsUnique());
            modelBuilder.Entity<SentYoudoOrder>(e => e.HasIndex(x => new { x.UserTelegramId, x.TaskId }).IsUnique());
            modelBuilder.Entity<SentFrOrder>(e => e.HasIndex(x => new { x.UserTelegramId, x.ProjectId }).IsUnique());
            modelBuilder.Entity<SentWsOrder>(e => e.HasIndex(x => new { x.UserTelegramId, x.TenderId }).IsUnique());

            modelBuilder.Entity<WorkspaceCategory>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.UserId, x.CategorySlug }).IsUnique();
                e.Property(x => x.Name).HasMaxLength(200);
                e.Property(x => x.NotificationInterval).HasMaxLength(20).HasDefaultValue("off");
                e.Property(x => x.CategorySlug).HasMaxLength(100);
                e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserKeywordFilter>(e =>
            {
                e.HasIndex(x => new { x.UserId, x.Platform, x.CategoryId });
                e.Property(x => x.Word).HasMaxLength(100);
                e.Property(x => x.Platform).HasMaxLength(20);
            });

            modelBuilder.Entity<AllParsedOrder>(e =>
            {
                e.HasIndex(x => new { x.Platform, x.ExternalId }).IsUnique();
                e.HasIndex(x => x.SavedAt);
                e.Property(x => x.Platform).HasMaxLength(20);
                e.Property(x => x.Title).HasMaxLength(500);
            });

            modelBuilder.Entity<ProfiCategory>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.UserId).IsRequired();

                e.Property(x => x.SearchQuery).HasMaxLength(200).IsRequired();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.NotificationInterval)
                    .HasMaxLength(20)
                    .HasDefaultValue("off");

                e.HasIndex(x => new { x.UserId, x.SearchQuery }).IsUnique();

                e.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SentProfiOrder>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.UserTelegramId, x.OrderId }).IsUnique();
            });
        }
    }
}
