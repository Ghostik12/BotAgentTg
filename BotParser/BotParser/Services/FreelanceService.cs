using BotParser.Db;
using BotParser.Models;
using BotParser.Parsers;
using Microsoft.EntityFrameworkCore;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace BotParser.Services
{
    public class FreelanceService
    {
        private readonly ITelegramBotClient _bot;
        private readonly KworkBotDbContext _db;
        private readonly KworkParser _kworkParser;
        private readonly FlParser _flParser;

        // Рубрики Kwork
        public static readonly Dictionary<int, string> KworkCategories = new()
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

        // Рубрики FL.ru (на основе твоих URL)
        public static readonly Dictionary<int, string> FlCategories = new()
        {
            { 0,  "Все проекты" },
            { 1,  "Сайты" },
            { 2,  "Дизайн" },
            { 3,  "Продвижение сайтов SEO" },
            { 4,  "Программирование" },
            { 5,  "Реклама и маркетинг" },
            { 6,  "Тексты" },
            { 7,  "Mobile" },
            { 8,  "3D графика" },
            { 9,  "Аутсорсинг и консалтинг" },
            { 10, "AI искусственный интеллект" },
            { 11, "Рисунки и иллюстрации" },
            { 12, "Crypto и blockchain" },
            { 13, "Инжиниринг" },
            { 14, "Аудио видео фото" },
            { 15, "Мессенджеры" },
            { 16,  "Анимация" },
            { 17,  "Макретплейс менеджмент" },
            { 18, "Автоматизация бизнеса" },
            { 19, "Игры" },
            { 20, "Фирменный стиль" },
            { 21, "Браузеры" },
            { 22, "Социальные сети" },
            { 23, "Интернет-магазины" }
        };

        public FreelanceService(ITelegramBotClient bot, KworkBotDbContext db, KworkParser kworkParser, FlParser flParser)
        {
            _bot = bot;
            _db = db;
            _kworkParser = kworkParser;
            _flParser = flParser;
        }

        // ─────────────────────── ГЛАВНОЕ МЕНЮ ───────────────────────
        public async Task ShowMainMenu(long chatId, int? messageId = null)
        {
            var buttons = new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Kwork.ru", "kwork_menu") },
                new[] { InlineKeyboardButton.WithCallbackData("FL.ru", "fl_menu") },
                new[] { InlineKeyboardButton.WithCallbackData("Мои подписки", "my_subscriptions") }
            };

            var markup = new InlineKeyboardMarkup(buttons);
            var text = "Главное меню:";

            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, replyMarkup: markup);
            else
                await _bot.SendMessage(chatId, text, replyMarkup: markup);
        }

        // ─────────────────────── МЕНЮ KWORK ───────────────────────
        public async Task ShowKworkMenu(long chatId, long userId, int? messageId = null)
        {
            var text = "<b>Kwork.ru</b>\n\nВыбери рубрики:";
            var buttons = KworkCategories.Select(kvp =>
                InlineKeyboardButton.WithCallbackData(kvp.Value, $"kwork_cat_{kvp.Key}"))
                .Select(b => new InlineKeyboardButton[] { b }).ToList();

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "main_menu") });

            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
        }

        public async Task ToggleKworkCategory(long userId, int catId, long chatId, string mId)
        {
            var existing = await _db.KworkCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId);
            if (existing != null)
            {
                _db.KworkCategories.Remove(existing);
                await _db.SaveChangesAsync();
                await _bot.AnswerCallbackQuery(mId, "Отключено");
            }
            else
            {
                _db.KworkCategories.Add(new KworkCategory
                {
                    UserId = userId,
                    CategoryId = catId,
                    Name = KworkCategories[catId],
                    NotificationInterval = "off"
                });
                await _db.SaveChangesAsync();
                await _bot.AnswerCallbackQuery(mId, "Включено");
            }

            await ShowKworkMenu(chatId, userId);
        }

        // ─────────────────────── МЕНЮ FL.RU ───────────────────────
        public async Task ShowFlMenu(long chatId, long userId, int? messageId = null)
        {
            var text = "<b>FL.ru</b>\n\nВыбери рубрики:";
            var buttons = FlCategories.Select(kvp =>
                InlineKeyboardButton.WithCallbackData(kvp.Value, $"fl_cat_{kvp.Key}"))
                .Select(b => new InlineKeyboardButton[] { b }).ToList();

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "main_menu") });

            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
        }

        public async Task ToggleFlCategory(long userId, int catId, long chatId, string mId)
        {
            var existing = await _db.FlCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId);
            if (existing != null)
            {
                _db.FlCategories.Remove(existing);
                await _db.SaveChangesAsync();
                await _bot.AnswerCallbackQuery(mId, "Отключено");
            }
            else
            {
                _db.FlCategories.Add(new FlCategory
                {
                    UserId = userId,
                    CategoryId = catId,
                    Name = FlCategories[catId],
                    NotificationInterval = "off"
                });
                await _db.SaveChangesAsync();
                await _bot.AnswerCallbackQuery(mId, "Включено");
            }

            await ShowFlMenu(chatId, userId);
        }

        // ─────────────────────── МОИ ПОДПИСКИ ───────────────────────
        public async Task ShowMySubscriptions(long chatId, long userId, int? messageId = null)
        {
            var kworkSubs = await _db.KworkCategories.Where(c => c.UserId == userId).ToListAsync();
            var flSubs = await _db.FlCategories.Where(c => c.UserId == userId).ToListAsync();

            if (!kworkSubs.Any() && !flSubs.Any())
            {
                var texts = "У тебя пока нет активных подписок";
                var markup = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Назад", "main_menu"));

                if (messageId.HasValue)
                    await _bot.EditMessageText(chatId, messageId.Value, texts, replyMarkup: markup);
                else
                    await _bot.SendMessage(chatId, texts, replyMarkup: markup);
                return;
            }

            var lines = new List<string> { $"<b>Мои подписки ({kworkSubs.Count + flSubs.Count})</b>\n\n" };

            if (kworkSubs.Any())
            {
                lines.Add("<b>Kwork.ru:</b>");
                foreach (var c in kworkSubs)
                    lines.Add($"  {GetStatus(c.NotificationInterval)} <b>{c.Name}</b> → {GetStatus(c.NotificationInterval)}");
                lines.Add("");
            }

            if (flSubs.Any())
            {
                lines.Add("<b>FL.ru:</b>");
                foreach (var c in flSubs)
                    lines.Add($"  {GetStatus(c.NotificationInterval)} <b>{c.Name}</b> → {GetStatus(c.NotificationInterval)}");
            }

            var text = string.Join("\n", lines);

            var buttons = new List<InlineKeyboardButton[]>();

            foreach (var c in kworkSubs)
            {
                var status = c.NotificationInterval == "off" ? "ВЫКЛ" : "ВКЛ";
                buttons.Add(new[]
                {
        InlineKeyboardButton.WithCallbackData(
            $"{status} {c.Name} → {GetPrettyInterval(c.NotificationInterval)}",
            $"edit_interval_kwork_{c.CategoryId}")
    });
            }

            foreach (var c in flSubs)
            {
                var status = c.NotificationInterval == "off" ? "ВЫКЛ" : "ВКЛ";
                buttons.Add(new[]
                {
        InlineKeyboardButton.WithCallbackData(
            $"{status} {c.Name} → {GetPrettyInterval(c.NotificationInterval)}",
            $"edit_interval_fl_{c.CategoryId}")
    });
            }

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "main_menu") });

            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons.ToArray()));
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons.ToArray()));
        }

        private string GetStatus(string interval) => interval == "off" ? "OFF" : "ON";

        // ─────────────────────── ОТПРАВКА ЗАКАЗОВ ───────────────────────
        public async Task SendKworkOrderAsync(long chatId, KworkParser.KworkOrder order)
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

            var text = $"<b>Kwork: {title}</b>{budgetText}\n\n{desc}\n\n<a href=\"{order.Url}\">{order.Url}</a>";

            await _bot.SendMessage(chatId, text, ParseMode.Html);
        }

        public async Task SendFlOrderAsync(long chatId, FlParser.FlOrder order)
        {
            var title = WebUtility.HtmlEncode(order.Title);
            var budgetText = order.Budget != null ? $"\nБюджет: <b>{order.Budget}</b>" : "";
            var desc = string.IsNullOrWhiteSpace(order.Description)
                ? "" : WebUtility.HtmlEncode(order.Description.Length > 400 ? order.Description[..400] + "…" : order.Description);

            var text = $"<b>FL.ru: {title}</b>{budgetText}\n\n{desc}\n\n<a href=\"{order.Url}\">{order.Url}</a>";

            await _bot.SendMessage(chatId, text, ParseMode.Html);
        }

        public async Task<List<KworkParser.KworkOrder>> GetKworkOrdersAsync(int? categoryId = null)
            => await _kworkParser.GetNewOrdersAsync(categoryId);

        public async Task<List<FlParser.FlOrder>> GetFlOrdersAsync(int? categoryId = null)
            => await _flParser.GetNewOrdersAsync(categoryId);

        private string GetPrettyInterval(string interval) => interval switch
        {
            "instant" => "Моментально",
            "5min" => "Раз в 5 минут",
            "15min" => "Раз в 15 минут",
            "hour" => "Раз в час",
            "day" => "Раз в день",
            "off" => "Выключено",
            _ => "Неизвестно"
        };

        public async Task ShowIntervalSelection(long chatId, long userId, int categoryId, bool isKwork, int? messageId = null)
        {
            var catName = isKwork
                ? KworkCategories.GetValueOrDefault(categoryId, "Неизвестная категория")
                : FlCategories.GetValueOrDefault(categoryId, "Неизвестная категория");

            var currentInterval = "off";
            if (isKwork)
            {
                var cat = await _db.KworkCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId);
                currentInterval = cat?.NotificationInterval ?? "off";
            }
            else
            {
                var cat = await _db.FlCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId);
                currentInterval = cat?.NotificationInterval ?? "off";
            }

            var prefix = isKwork ? "kwork" : "fl";

            var buttons = new[]
            {
        new[] { InlineKeyboardButton.WithCallbackData("Моментально (каждая минута)", $"{prefix}_setint_{categoryId}_instant") },
        new[] { InlineKeyboardButton.WithCallbackData("Раз в 5 минут", $"{prefix}_setint_{categoryId}_5min") },
        new[] { InlineKeyboardButton.WithCallbackData("Раз в 15 минут", $"{prefix}_setint_{categoryId}_15min") },
        new[] { InlineKeyboardButton.WithCallbackData("Раз в час", $"{prefix}_setint_{categoryId}_hour") },
        new[] { InlineKeyboardButton.WithCallbackData("Раз в день", $"{prefix}_setint_{categoryId}_day") },
        new[] { InlineKeyboardButton.WithCallbackData("Выключить", $"{prefix}_setint_{categoryId}_off") },
        new[] { InlineKeyboardButton.WithCallbackData("Назад в подписки", "my_subscriptions") }
    };

            var text = $"<b>Настройка уведомлений</b>\n\nКатегория: <b>{catName}</b>\nТекущий режим: <b>{GetPrettyInterval(currentInterval)}</b>";

            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons));
        }

        public async Task SetNotificationInterval(long userId, int categoryId, string interval, bool isKwork)
        {
            if (isKwork)
            {
                var cat = await _db.KworkCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId);
                if (cat != null)
                {
                    cat.NotificationInterval = interval;
                    await _db.SaveChangesAsync();
                }
            }
            else
            {
                var cat = await _db.FlCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId);
                if (cat != null)
                {
                    cat.NotificationInterval = interval;
                    await _db.SaveChangesAsync();
                }
            }
        }

        public async Task EnsureUserExists(long userId, string? username = null)
        {
            var exists = await _db.Users.AnyAsync(u => u.Id == userId);
            if (!exists)
            {
                _db.Users.Add(new Models.User
                {
                    Id = userId,
                    Username = username,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
        }
    }
}
