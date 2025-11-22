using BotParser.Db;
using BotParser.Models;
using BotParser.Parsers;
using Microsoft.EntityFrameworkCore;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static BotParser.Parsers.KworkParser;
using static System.Net.Mime.MediaTypeNames;

namespace BotParser.Services
{
    public class KworkService
    {
        private readonly ITelegramBotClient _bot;
        private readonly KworkBotDbContext _db;
        private readonly KworkParser _parser;

        public static readonly Dictionary<int, string> Categories = new()
    {
        { 0,  "Все категории" },
        { 11, "Разработка и IT" },
        { 15, "Дизайн" },
        { 5,  "Тексты и переводы" },
        { 17, "SEO и трафик" },
        { 45, "Соцсети и маркетинг" },
        { 7,  "Аудио, видео, съёмка" },
        { 83, "Бизнес и жизнь" }
    };

        public KworkService(ITelegramBotClient bot, KworkBotDbContext db, KworkParser parser)
        {
            _bot = bot;
            _db = db;
            _parser = parser;
        }

        public async Task ShowMainMenu(long chatId, int? messageId = null)
        {
            var buttons = new[]
            {
            new[] { InlineKeyboardButton.WithCallbackData("Выбрать рубрики", "show_categories") },
            new[] { InlineKeyboardButton.WithCallbackData("Мои подписки", "my_subscriptions") }
        };

            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, "Главное меню:", replyMarkup: new InlineKeyboardMarkup(buttons));
            else
                await _bot.SendMessage(chatId, "Главное меню:", replyMarkup: new InlineKeyboardMarkup(buttons));
        }

        // Вспомогательная функция — имя интервала
        private string GetIntervalName(string code) => code switch
        {
            "instant" => "Мгновенно",
            "15min" => "Каждые 15 минут",
            "hour" => "Каждый час",
            "day" => "Раз в сутки",
            _ => "Отключено"
        };

        // Удаление сообщения (безопасно)
        public async Task TryDeleteMessage(long chatId, int messageId)
        {
            try { await _bot.DeleteMessage(chatId, messageId); }
            catch { }
        }

        // ──────────────────────────────────────────────────────────────
        // 1. Выбор рубрики — 100% без ошибок
        public async Task SelectCategory(long userId, int catId, long chatId, int messageId)
        {
            var user = await _db.Users
                .Include(u => u.SelectedCategories)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                user = new Models.User { Id = userId };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }

            // Удаляем старую подписку на эту рубрику
            var existing = user.SelectedCategories.FirstOrDefault(c => c.CategoryId == catId);
            if (existing != null)
                user.SelectedCategories.Remove(existing);

            // Создаём новую
            var newCategory = new KworkCategory
            {
                UserId = userId,
                CategoryId = catId,
                Name = Categories[catId],
                NotificationInterval = "off"
            };

            user.SelectedCategories.Add(newCategory);
            await _db.SaveChangesAsync();

            await TryDeleteMessage(chatId, messageId);

            // Передаём объект напрямую — без поиска!
            await ShowIntervalForCategory(chatId, newCategory);
        }

        // ──────────────────────────────────────────────────────────────
        // 2. Показ интервалов — принимает готовый объект
        public async Task ShowIntervalForCategory(long chatId, KworkCategory category)
        {
            var intervals = new (string text, string code)[]
            {
            ("Мгновенно", "instant"),
            ("Каждые 15 минут", "15min"),
            ("Каждый час", "hour"),
            ("Раз в сутки", "day"),
            ("Отключить", "off")
            };

            var buttons = new List<InlineKeyboardButton[]>();

            foreach (var i in intervals)
            {
                var mark = category.NotificationInterval == i.code ? " ON" : "";
                buttons.Add(new[]
                {
                InlineKeyboardButton.WithCallbackData(i.text + mark, $"set_interval_{category.CategoryId}_{i.code}")
            });
            }

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Главное меню", "main_menu") });
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад к рубрикам", "show_categories") });

            await _bot.SendMessage(
                chatId: chatId,
                text: $"<b>{category.Name}</b>\n\nВыбери, как часто присылать заказы:",
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(buttons)
            );
        }

        // ──────────────────────────────────────────────────────────────
        // 3. Установка интервала
        public async Task SetIntervalForCategory(long userId, int catId, string interval, long chatId, int? messageId = null)
        {
            var category = await _db.KworkCategories
                .FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId);

            if (category != null)
            {
                category.NotificationInterval = interval;
                await _db.SaveChangesAsync();
            }

            await _bot.SendMessage(chatId,
                $"Рубрика «{Categories[catId]}» → {GetIntervalName(interval)} ON");

            await Task.Delay(1200);

            // Обновляем список рубрик
            await ShowCategories(chatId, userId, messageId);
        }

        // ──────────────────────────────────────────────────────────────
        // 4. Показ списка рубрик
        public async Task ShowCategories(long chatId, long userId, int? messageId = null)
        {
            var user = await _db.Users.Include(u => u.SelectedCategories)
                                      .FirstOrDefaultAsync(u => u.Id == userId);

            var selected = user?.SelectedCategories.Select(c => c.CategoryId).ToHashSet() ?? new HashSet<int>();

            var buttons = Categories.Select(kvp =>
                InlineKeyboardButton.WithCallbackData(
                    selected.Contains(kvp.Key) ? "ON " + kvp.Value : kvp.Value,
                    $"select_cat_{kvp.Key}"
                )).Select(b => new[] { b }).ToList();

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Главное меню", "main_menu") });

            var text = "<b>Выбери рубрики для отслеживания:</b>";

            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
        }

        // Отправка заказа (остаётся как было, но красивее)
        public async Task SendOrderAsync(long chatId, KworkParser.KworkOrder order)
        {
            var title = WebUtility.HtmlEncode(order.Title);
            var budget = new List<string>();
            if (!string.IsNullOrEmpty(order.DesiredBudget))
                budget.Add($"Желаемый: <b>{order.DesiredBudget.Trim()}</b>");
            if (!string.IsNullOrEmpty(order.AllowedBudget))
                budget.Add($"Допустимый: <b>{order.AllowedBudget.Trim()}</b>");

            var budgetText = budget.Any() ? "\n" + string.Join("\n", budget) : "";
            var desc = string.IsNullOrWhiteSpace(order.Description)
                ? "" : WebUtility.HtmlEncode(order.Description.Length > 400 ? order.Description[..400] + "…" : order.Description);

            var text = $"<b>{title}</b>{budgetText}\n\n{desc}\n\n<a href=\"{order.Url}\">{order.Url}</a>";

            await _bot.SendMessage(chatId, text, ParseMode.Html);
        }

        // Проверка и отправка новых заказов (для авто и ручной)
        public async Task CheckAndSendNewOrders(long userTelegramId)
        {
            var user = await _db.Users.Include(u => u.SelectedCategories)
                                      .FirstOrDefaultAsync(u => u.Id == userTelegramId);
            if (user == null || !user.SelectedCategories.Any()) return;

            var allNewOrders = new List<KworkParser.KworkOrder>();
            foreach (var cat in user.SelectedCategories)
            {
                var orders = await _parser.GetNewOrdersAsync(cat.CategoryId == 0 ? null : cat.CategoryId);
                allNewOrders.AddRange(orders);
            }

            if (!allNewOrders.Any()) return;

            var sentIds = await _db.SentOrders
                .Where(s => s.UserTelegramId == userTelegramId)
                .Select(s => s.ProjectId)
                .ToHashSetAsync();

            var newOnes = allNewOrders.Where(o => !sentIds.Contains(o.ProjectId)).ToList();

            foreach (var order in newOnes)
            {
                await SendOrderAsync(userTelegramId, order);
                _db.SentOrders.Add(new SentOrder
                {
                    ProjectId = order.ProjectId,
                    UserTelegramId = userTelegramId
                });
                await Task.Delay(1100);
            }

            if (newOnes.Any())
                await _db.SaveChangesAsync();
        }

        public async Task<Models.User> GetOrCreateUser(long id, string username)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                user = new Models.User { Id = id, Username = username };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }
            return user;
        }
    }
}
