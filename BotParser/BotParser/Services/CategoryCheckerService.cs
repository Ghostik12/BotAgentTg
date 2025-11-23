using BotParser.Db;
using BotParser.Models;
using BotParser.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static BotParser.Parsers.FlParser;


namespace BotParser.Services
{
    public class CategoryCheckerService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<CategoryCheckerService> _log;
        private readonly Dictionary<string, DateTime> _lastCheckTimes = new();
        private readonly FreelanceService _freelance;

        public CategoryCheckerService(IServiceProvider sp, ILogger<CategoryCheckerService> log, FreelanceService freelance)
        {
            _sp = sp;
            _log = log;
            _freelance = freelance;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _log.LogInformation("CategoryCheckerService запущен");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await CheckKworkSubscriptions(ct);
                    await CheckFlSubscriptions(ct);
                    await CheckYoudoSubscriptions(ct);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Критическая ошибка в CategoryCheckerService");
                }

                await Task.Delay(TimeSpan.FromSeconds(22), ct);
            }
        }

        private async Task CheckYoudoSubscriptions(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KworkBotDbContext>();
            var parser = scope.ServiceProvider.GetRequiredService<YoudoParser>();

            var subs = await db.YoudoCategories
                .Where(c => c.NotificationInterval != "off")
                .ToListAsync(ct);

            foreach (var sub in subs)
            {
                var key = $"youdo_{sub.UserId}_{sub.CategoryId}";
                var minutes = IntervalToMinutes(sub.NotificationInterval);
                if (minutes <= 0) continue;

                var last = _lastCheckTimes.GetValueOrDefault(key, DateTime.MinValue);
                if ((DateTime.UtcNow - last).TotalMinutes < minutes) continue;

                try
                {
                    var orders = await parser.GetNewOrdersAsync(sub.CategoryId == 0 ? null : sub.CategoryId);
                    var sentIds = await db.SentYoudoOrders
                        .Where(s => s.UserTelegramId == sub.UserId)
                        .Select(s => s.TaskId)
                        .ToHashSetAsync(ct);

                    var newOrders = orders.Where(o => !sentIds.Contains(o.TaskId)).ToList();

                    foreach (var order in newOrders)
                    {
                        await _freelance.SendYoudoOrderAsync(sub.UserId, order); // Добавь метод в FreelanceService
                        db.SentYoudoOrders.Add(new SentYoudoOrder { TaskId = order.TaskId, UserTelegramId = sub.UserId });
                        await Task.Delay(1100, ct);
                    }

                    if (newOrders.Any()) await db.SaveChangesAsync(ct);
                    _lastCheckTimes[key] = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    // Лог
                }
            }
        }

        private async Task CheckKworkSubscriptions(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KworkBotDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<FreelanceService>();

            var subs = await db.KworkCategories
                .Where(c => c.NotificationInterval != "off")
                .ToListAsync(ct);

            foreach (var sub in subs)
            {
                var key = $"kwork_{sub.UserId}_{sub.CategoryId}";
                var minutes = IntervalToMinutes(sub.NotificationInterval);
                if (minutes <= 0) continue;

                var last = _lastCheckTimes.GetValueOrDefault(key, DateTime.MinValue);
                if ((DateTime.UtcNow - last).TotalMinutes < minutes) continue;

                try
                {
                    var orders = await service.GetKworkOrdersAsync(sub.CategoryId == 0 ? null : sub.CategoryId);
                    var sentIds = await db.SentOrders
                        .Where(s => s.UserTelegramId == sub.UserId)
                        .Select(s => s.ProjectId)
                        .ToHashSetAsync(ct);

                    var newOrders = orders.Where(o => !sentIds.Contains(o.ProjectId)).ToList();

                    foreach (var order in newOrders)
                    {
                        await service.SendKworkOrderAsync(sub.UserId, order);
                        db.SentOrders.Add(new SentOrder
                        {
                            ProjectId = order.ProjectId,
                            UserTelegramId = sub.UserId
                        });
                        await Task.Delay(1100, ct);
                    }

                    if (newOrders.Any())
                        await db.SaveChangesAsync(ct);

                    _lastCheckTimes[key] = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Ошибка парсинга Kwork для пользователя {UserId}, категория {CatId}", sub.UserId, sub.CategoryId);
                }
            }
        }

        private async Task CheckFlSubscriptions(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KworkBotDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<FreelanceService>();

            var subs = await db.FlCategories
                .Where(c => c.NotificationInterval != "off")
                .ToListAsync(ct);

            foreach (var sub in subs)
            {
                var key = $"fl_{sub.UserId}_{sub.CategoryId}";
                var minutes = IntervalToMinutes(sub.NotificationInterval);
                if (minutes <= 0) continue;

                var last = _lastCheckTimes.GetValueOrDefault(key, DateTime.MinValue);
                if ((DateTime.UtcNow - last).TotalMinutes < minutes) continue;

                try
                {
                    var orders = await service.GetFlOrdersAsync(sub.CategoryId == 0 ? null : sub.CategoryId);
                    var sentIds = await db.SentFlOrders
                        .Where(s => s.UserTelegramId == sub.UserId)
                        .Select(s => s.ProjectId)
                        .ToHashSetAsync(ct);

                    var newOrders = orders.Where(o => !sentIds.Contains(o.ProjectId)).ToList();

                    foreach (var order in newOrders)
                    {
                        await service.SendFlOrderAsync(sub.UserId, order);
                        db.SentFlOrders.Add(new SentFlOrder
                        {
                            ProjectId = order.ProjectId,
                            UserTelegramId = sub.UserId
                        });
                        await Task.Delay(1100, ct);
                    }

                    if (newOrders.Any())
                        await db.SaveChangesAsync(ct);

                    _lastCheckTimes[key] = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Ошибка парсинга FL.ru для пользователя {UserId}, категория {CatId}", sub.UserId, sub.CategoryId);
                }
            }
        }

        private static int IntervalToMinutes(string interval) => interval switch
        {
            "instant" => 1,
            "5min" => 5,
            "15min" => 15,
            "hour" => 60,
            "day" => 1440,
            _ => 0
        };
    }
}
