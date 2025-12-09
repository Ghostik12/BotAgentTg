using BotParser.Db;
using BotParser.Models;
using BotParser.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;


namespace BotParser.Services
{
    public class CategoryCheckerService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<CategoryCheckerService> _log;
        private readonly Dictionary<string, DateTime> _lastCheckTimes = new();
        private readonly FreelanceService _freelance;
        private readonly MobileProxyService _proxyService;
        private DateTime _lastRotation = DateTime.MinValue;

        public CategoryCheckerService(IServiceProvider sp, ILogger<CategoryCheckerService> log, FreelanceService freelance, MobileProxyService mobileProxyService)
        {
            _sp = sp;
            _log = log;
            _freelance = freelance;
            _proxyService = mobileProxyService;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _log.LogInformation("CategoryCheckerService запущен");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // РОТАЦИЯ IP КАЖДЫЕ 10 МИНУТ
                    if (DateTime.UtcNow - _lastRotation > TimeSpan.FromMinutes(10))
                    {
                        var newIp = await _proxyService.RotateAndVerifyIpAsync(_log);
                        if (newIp != null)
                            _lastRotation = DateTime.UtcNow;
                        else
                            _lastRotation = DateTime.UtcNow.AddMinutes(3); // повтор через 3 мин, если не вышло
                    }

                    await CheckKworkSubscriptions(ct);
                    await CheckFlSubscriptions(ct);
                    await CheckYoudoSubscriptions(ct);
                    await CheckFrSubscriptions(ct);
                    await CheckWorkspaceSubscriptions(ct);
                    await CheckProfiSubscriptions(ct);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex,
                    "КРИТИЧЕСКАЯ ОШИБКА в CategoryCheckerService | Время: {Time} | Стек: {StackTrace}",
                    DateTime.Now, ex.StackTrace);
                }

                await Task.Delay(TimeSpan.FromSeconds(60), ct);
            }
        }

        private async Task CheckProfiSubscriptions(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KworkBotDbContext>();
            var parser = scope.ServiceProvider
                .GetRequiredService<Func<long, ProfiRuParser>>()
                .Invoke(user.TelegramId);
            var freelance = scope.ServiceProvider.GetRequiredService<FreelanceService>();

            var subs = await db.ProfiCategories
                .Where(c => c.NotificationInterval != "off")
                .ToListAsync(ct);

            foreach (var sub in subs)
            {
                var key = $"profi_{sub.UserId}_{sub.Id}";
                var minutes = IntervalToMinutes(sub.NotificationInterval);
                if (minutes <= 0) continue;
                if ((DateTime.UtcNow - _lastCheckTimes.GetValueOrDefault(key, DateTime.MinValue)).TotalMinutes < minutes) continue;

                // Берём запрос из словаря по CategoryId
                //var query = FreelanceService.ProfiQueries[sub.Id];
                var query = db.ProfiCategories.Where(c => c.Id == sub.Id).Select(c => c.SearchQuery).ToArray();
                var orders = await parser.GetOrdersAsync(query[0]);

                var sentIds = await db.SentProfiOrders
                    .Where(s => s.UserTelegramId == sub.UserId)
                    .Select(s => s.OrderId)
                    .ToHashSetAsync(ct);

                foreach (var order in orders)
                {
                    if (sentIds.Contains(order.OrderId)) continue;

                    bool matches = await freelance.TitleContainsKeyword(
                        sub.UserId,
                        "profi",
                        sub.Id,
                        order.Title + " " + order.Description);

                    if (!matches) continue;

                    await freelance.SendProfiOrderAsync(sub.UserId, order, sub.Id);

                    await db.SentProfiOrders.AddAsync(new SentProfiOrder
                    {
                        OrderId = order.OrderId,
                        UserTelegramId = sub.UserId,
                        SentAt = DateTime.UtcNow,
                        
                    });

                    await Task.Delay(600, ct);
                }

                if (orders.Any()) await db.SaveChangesAsync(ct);
                _lastCheckTimes[key] = DateTime.UtcNow;
            }
        }

        private async Task CheckWorkspaceSubscriptions(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KworkBotDbContext>();
            var parser = scope.ServiceProvider.GetRequiredService<WorkspaceRuParser>();
            var freelance = scope.ServiceProvider.GetRequiredService<FreelanceService>();

            var subs = await db.WorkspaceCategories
                .Where(c => c.NotificationInterval != "off")
                .ToListAsync(ct);

            foreach (var sub in subs)
            {
                var key = $"ws_{sub.UserId}_{sub.CategorySlug}";
                var minutes = IntervalToMinutes(sub.NotificationInterval);
                if (minutes <= 0) continue;
                if ((DateTime.UtcNow - _lastCheckTimes.GetValueOrDefault(key, DateTime.MinValue)).TotalMinutes < minutes) continue;

                var slug = sub.CategorySlug == 1 ? 1 : sub.CategorySlug;
                var tenders = await parser.GetActiveTendersAsync(slug);
                var sent = await db.SentWsOrders
                    .Where(s => s.UserTelegramId == sub.UserId)
                    .Select(s => s.TenderId)
                    .ToHashSetAsync(ct);

                var newTenders = tenders.Where(t => !sent.Contains(t.TenderId)).Take(7).ToList();

                foreach (var tender in newTenders)
                {
                    // 1. Уже отправляли этому пользователю?
                    if (sent.Contains(tender.TenderId)) continue;

                    // 2. Проверяем ключевые слова ТОЛЬКО для этой рубрики
                    bool matches = await _freelance.TitleContainsKeyword(
                        sub.UserId,
                        "workspace",
                        sub.CategorySlug,
                        tender.Title);

                    if (!matches) continue; // ← НЕ присылаем, если не подходит

                    // 3. Отправляем!
                    await _freelance.SendWsOrderAsync(sub.UserId, tender);

                    await db.SentWsOrders.AddAsync(new SentWsOrder
                    {
                        TenderId = tender.TenderId,
                        UserTelegramId = sub.UserId,
                        SentAt = DateTime.UtcNow,
                    });

                    await Task.Delay(1000, ct);
                }

                if (newTenders.Any()) await db.SaveChangesAsync(ct);
                _lastCheckTimes[key] = DateTime.UtcNow;
            }
        }

        private async Task CheckFrSubscriptions(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KworkBotDbContext>();
            var parser = scope.ServiceProvider.GetRequiredService<FreelanceRuParser>();
            var freelance = scope.ServiceProvider.GetRequiredService<FreelanceService>();

            var subs = await db.FrCategories
                .Where(c => c.NotificationInterval != "off" && c.NotificationInterval != null)
                .ToListAsync(ct);

            foreach (var sub in subs)
            {
                var key = $"fr_{sub.UserId}_{sub.CategoryId}";
                var minutes = IntervalToMinutes(sub.NotificationInterval);
                if (minutes <= 0) continue;

                var lastCheck = _lastCheckTimes.GetValueOrDefault(key, DateTime.MinValue);
                if ((DateTime.UtcNow - lastCheck).TotalMinutes < minutes) continue;

                try
                {
                    var orders = await parser.GetNewOrdersAsync(sub.CategoryId == 0 ? null : (int?)sub.CategoryId);
                    var sentIds = await db.SentFrOrders
                        .Where(s => s.UserTelegramId == sub.UserId)
                        .Select(s => s.ProjectId)
                        .ToHashSetAsync(ct);

                    var newOrders = orders.Where(o => !sentIds.Contains(o.ProjectId)).Take(5).ToList();

                    foreach (var tender in newOrders)
                    {
                        // 1. Уже отправляли этому пользователю?
                        if (sentIds.Contains(tender.ProjectId)) continue;

                        // 2. Проверяем ключевые слова ТОЛЬКО для этой рубрики
                        bool matches = await _freelance.TitleContainsKeyword(
                            sub.UserId,
                            "freelance",
                            sub.CategoryId,
                            tender.Title);

                        if (!matches) continue; // ← НЕ присылаем, если не подходит

                        // 3. Отправляем!
                        await _freelance.SendFrOrderAsync(sub.UserId, tender);

                        await db.SentFrOrders.AddAsync(new SentFrOrder
                        {
                            ProjectId = tender.ProjectId,
                            UserTelegramId = sub.UserId
                        });

                        await Task.Delay(1000, ct);
                    }

                    if (newOrders.Any())
                        await db.SaveChangesAsync(ct);

                    _lastCheckTimes[key] = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка парсинга Freelance.ru: {ex.Message}");
                }
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

                    foreach (var tender in newOrders)
                    {
                        // 1. Уже отправляли этому пользователю?
                        if (sentIds.Contains(tender.TaskId)) continue;

                        // 2. Проверяем ключевые слова ТОЛЬКО для этой рубрики
                        bool matches = await _freelance.TitleContainsKeyword(
                            sub.UserId,
                            "youdo",
                            sub.CategoryId,
                            tender.Title);

                        if (!matches) continue; // ← НЕ присылаем, если не подходит

                        // 3. Отправляем!
                        await _freelance.SendYoudoOrderAsync(sub.UserId, tender);

                        await db.SentYoudoOrders.AddAsync(new SentYoudoOrder
                        {
                            TaskId = tender.TaskId,
                            UserTelegramId = sub.UserId
                        });

                        await Task.Delay(1000, ct);
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

                    foreach (var tender in newOrders)
                    {
                        // 1. Уже отправляли этому пользователю?
                        if (sentIds.Contains(tender.ProjectId)) continue;

                        // 2. Проверяем ключевые слова ТОЛЬКО для этой рубрики
                        bool matches = await _freelance.TitleContainsKeyword(
                            sub.UserId,
                            "kwork",
                            sub.CategoryId,
                            tender.Title);

                        if (!matches) continue; // ← НЕ присылаем, если не подходит

                        // 3. Отправляем!
                        await _freelance.SendKworkOrderAsync(sub.UserId, tender);

                        await db.SentOrders.AddAsync(new SentOrder
                        {
                            ProjectId = tender.ProjectId,
                            UserTelegramId = sub.UserId
                        });

                        await Task.Delay(1000, ct);
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

                    foreach (var tender in newOrders)
                    {
                        // 1. Уже отправляли этому пользователю?
                        if (sentIds.Contains(tender.ProjectId)) continue;

                        // 2. Проверяем ключевые слова ТОЛЬКО для этой рубрики
                        bool matches = await _freelance.TitleContainsKeyword(
                            sub.UserId,
                            "fl",
                            sub.CategoryId,
                            tender.Title);

                        if (!matches) continue; // ← НЕ присылаем, если не подходит

                        // 3. Отправляем!
                        await _freelance.SendFlOrderAsync(sub.UserId, tender);

                        await db.SentFlOrders.AddAsync(new SentFlOrder
                        {
                            ProjectId = tender.ProjectId,
                            UserTelegramId = sub.UserId
                        });

                        await Task.Delay(1000, ct);
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
