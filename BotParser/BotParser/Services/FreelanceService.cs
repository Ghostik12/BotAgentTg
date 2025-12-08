using BotParser.Db;
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

        public static readonly Dictionary<int, string> YoudoCategories = new()
        {
            { 0,   "Все задания" },
            { 1,   "Курьерские услуги" },
            { 2,   "Ремонт и строительство" },
            { 3,   "Грузоперевозки" },
            { 4,   "Уборка и помощь по хозяйству" },
            { 5,   "Виртуальный помощник" },
            { 6,   "Компьютерная помощь" },
            { 7,   "Мероприятия и промоакции" },
            { 8,   "Дизайн" },
            { 9,   "Разработка ПО" },
            { 10,  "Фото, видео и аудио" },
            { 11,  "Установка и ремонт техники" },
            { 12,  "Красота и здоровье" },
            { 13,  "Ремонт цифровой техники" },
            { 14,  "Юридическая и бухгалтерская помощь" },
            { 15,  "Репетиторы и обучение" },
            { 16,  "Ремонт транспорта" },
            { 17,  "Ручные работы и хендмейд" },
            { 18,  "Праздники и мероприятия" },
            { 19,  "Другое" }
        };

        public static readonly Dictionary<int, string> FrCategories = new()
        {
            { 0,    "Все проекты" },
            { 577,  "3D-графика" },
            { 590,  "Арт и иллюстрации" },
            { 133,  "Аутсорсинг и консалтинг" },
            { 116,  "Веб-разработка и Продуктовый дизайн" },
            { 40,   "Графический дизайн" },
            { 716,  "Дизайн пространства" },
            { 186,  "Инженерия" },
            { 673,  "Интернет-продвижение" },
            { 724,  "Искусственный интеллект" },
            { 4,    "IT и разработка" },
            { 117,  "Маркетинг и реклама" },
            { 565,  "Медиа и моушен дизайн" },
            { 89,   "Музыка и звук" },
            { 663,  "Обучение и образование" },
            { 29,   "Переводы" },
            { 124,  "Тексты" },
            { 98,   "Фотография" }
        };

        public static readonly Dictionary<int, string> WsCategories = new()
        {
            { 1,           "Все тендеры" },
            { 2,   "Разработка сайтов" },
            { 3,                "SEO" },
            { 4,            "Контекстная реклама" },
            { 5,  "Мобильные приложения" },
            { 6,                "SMM и PR" },
            { 7,          "Маркетинг" },
            { 8,           "Дизайн и бренд" },
            { 9,        "Контент и копирайтинг" },
            { 10,                "CRM, 1C, ПО, боты" },
            { 11,            "Игры" },
            { 12,    "Видео и фото" }
        };

        public static readonly Dictionary<int, string?> CategoryIdToSlug = new()
        {
            { 1, null },                    // Все тендеры
            { 2, "web-development" },
            { 3, "seo" },
            { 4, "context" },
            { 5, "apps-development" },
            { 6, "smm" },
            { 7, "marketing" },
            { 8, "identity" },
            { 9, "copywriting" },
            { 10, "crm" },
            { 11, "gamedev" },
            { 12, "videoproduction" }
        };

        public FreelanceService(ITelegramBotClient bot, KworkBotDbContext db, KworkParser kworkParser, FlParser flParser)
        {
            _bot = bot;
            _db = db;
            _kworkParser = kworkParser;
            _flParser = flParser;
        }

        public async Task<bool> TitleContainsKeyword(long userId, string platform, int categoryId, string title)
        {
            var keywords = await _db.UserKeywordFilters
                .Where(k => k.UserId == userId &&
                            k.Platform == platform &&
                            k.CategoryId == categoryId)
                .Select(k => k.Word.ToLower())
                .ToListAsync();

            if (keywords.Count == 0) return true; // нет слов — присылаем всё

            var lowerTitle = title.ToLower();
            return keywords.Any(word => lowerTitle.Contains(word));
        }

        // ─────────────────────── ГЛАВНОЕ МЕНЮ ───────────────────────
        public async Task ShowMainMenu(long chatId, int? messageId = null)
        {
            var buttons = new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Kwork.ru", "kwork_menu"),
                InlineKeyboardButton.WithCallbackData("FL.ru", "fl_menu") },
                new[] { InlineKeyboardButton.WithCallbackData("YouDo.com", "youdo_menu"),
                InlineKeyboardButton.WithCallbackData("Freelance.ru", "fr_menu") },
                new[] { InlineKeyboardButton.WithCallbackData("Workspace.ru", "workspace_menu"),
                InlineKeyboardButton.WithCallbackData("Profi.ru", "profi_menu") },
                new[] { InlineKeyboardButton.WithCallbackData("Мои подписки", "my_subscriptions") }
            };

            var markup = new InlineKeyboardMarkup(buttons);
            var text = "Главное меню:";

            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, replyMarkup: markup);
            else
                await _bot.SendMessage(chatId, text, replyMarkup: markup);
        }

        // ─────────────────────── МЕНЮ PROFI ───────────────────────
        public async Task ShowProfiMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.ProfiCategories.Where(c => c.UserId == userId).ToListAsync();
            var buttons = new List<InlineKeyboardButton[]>();

            var text = "<b>Profi.ru — персональный поиск</b>\n\n" +
                       "Ты сам создаёшь запросы — получаешь только нужные заказы.\n\n" +
                       "Примеры:\n" +
                       "• битрикс\n" +
                       "• telegram бот\n" +
                       "• nuxt vue сайт\n" +
                       "• лендинг за 100к";
            if (subs != null)
            {
                foreach (var sub in subs)
                    if (sub.NotificationInterval == "off")
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"✅ Выкл {sub.Name}", $"profi_cat_{sub.Id}") });
                    else
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"✅ {GetPrettyInterval(sub.NotificationInterval)} {sub.Name}", $"profi_cat_{sub.Id}") });
            }

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Добавить свой поиск", "profi_add_custom") });
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "main_menu") });

            var markup = new InlineKeyboardMarkup(buttons);

            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: markup);
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: markup);
        }

        // ─────────────────────── МЕНЮ KWORK ───────────────────────
        public async Task ShowKworkMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.KworkCategories.Where(c => c.UserId == userId).ToListAsync();
            var text = "<b>Kwork.ru</b>\n\nВыбери категории (статус подписки):";
            var buttons = new List<InlineKeyboardButton[]>();

            foreach (var kvp in KworkCategories)
            {
                var sub = subs.FirstOrDefault(s => s.CategoryId == kvp.Key);

                string status;
                if (sub == null)
                    status = "❌ OFF";
                else if (sub.NotificationInterval == "off")
                    status = "✅ Выкл";
                else
                {
                    var full = $"✅ {GetPrettyInterval(sub.NotificationInterval)}";
                    status = full.Length > 10 ? full.Substring(0, 8) + ".." : full;
                }

                buttons.Add(new[]
                {
            InlineKeyboardButton.WithCallbackData($"{status} {kvp.Value}", $"kwork_cat_{kvp.Key}")
        });
            }

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "main_menu") });

            var markup = new InlineKeyboardMarkup(buttons.ToArray());
            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: markup);
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: markup);
        }

        // ─────────────────────── МЕНЮ FL.RU ───────────────────────
        public async Task ShowFlMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.FlCategories.Where(c => c.UserId == userId).ToListAsync();
            var text = "<b>FL.ru</b>\n\nВыбери категории (статус подписки):";
            var buttons = new List<InlineKeyboardButton[]>();

            foreach (var kvp in FlCategories)
            {
                var sub = subs.FirstOrDefault(s => s.CategoryId == kvp.Key);

                string status;
                if (sub == null)
                    status = "❌ OFF";
                else if (sub.NotificationInterval == "off")
                    status = "✅ Выкл";
                else
                {
                    var full = $"✅ {GetPrettyInterval(sub.NotificationInterval)}";
                    status = full.Length > 10 ? full.Substring(0, 8) + ".." : full;
                }

                buttons.Add(new[]
                {
            InlineKeyboardButton.WithCallbackData($"{status} {kvp.Value}", $"fl_cat_{kvp.Key}")
        });
            }

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "main_menu") });

            var markup = new InlineKeyboardMarkup(buttons.ToArray());
            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: markup);
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: markup);
        }

        // ─────────────────────── МОИ ПОДПИСКИ ───────────────────────
        public async Task ShowMySubscriptions(long chatId, long userId, int? messageId = null)
        {
            var kworkSubs = await _db.KworkCategories.Where(c => c.UserId == userId).ToListAsync();
            var flSubs = await _db.FlCategories.Where(c => c.UserId == userId).ToListAsync();
            var youDo = await _db.YoudoCategories.Where(c => c.UserId == userId).ToListAsync();
            var frSubs = await _db.FrCategories.Where(c => c.UserId == userId).ToListAsync();
            var wsSubs = await _db.WorkspaceCategories.Where(c => c.UserId == userId).ToListAsync();
            var profiSubs = await _db.ProfiCategories.Where(c => c.UserId == userId).ToListAsync();

            if (!kworkSubs.Any() && !flSubs.Any() && !youDo.Any() && !frSubs.Any() && !wsSubs.Any() && !profiSubs.Any())
            {
                var texts = "У тебя пока нет активных подписок";
                var markup = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Назад", "main_menu"));

                if (messageId.HasValue)
                    await _bot.EditMessageText(chatId, messageId.Value, texts, replyMarkup: markup);
                else
                    await _bot.SendMessage(chatId, texts, replyMarkup: markup);
                return;
            }

            var lines = new List<string> { $"<b>Мои подписки ({kworkSubs.Count + flSubs.Count + youDo.Count + frSubs.Count + wsSubs.Count + profiSubs.Count})</b>\n\n" };

            if (kworkSubs.Any())
            {
                lines.Add("<b>Kwork.ru: 1️⃣</b>");
            }

            if (flSubs.Any())
            {
                lines.Add("<b>FL.ru: 2️⃣</b>");
            }

            if (youDo.Any())
            {
                lines.Add("<b>Youdo.com: 3️⃣</b>");
            }

            if (frSubs.Any())
            {
                lines.Add("<b>Freelance.ru: 4️⃣</b>");
            }

            if (wsSubs.Any())
            {
                lines.Add("<b>Workspace.ru: 5️⃣</b>");
            }

            if (wsSubs.Any())
            {
                lines.Add("<b>Profi.ru: 6️⃣</b>");
            }

            var text = string.Join("\n", lines);

            var buttons = new List<InlineKeyboardButton[]>();

            foreach (var c in kworkSubs)
            {
                var status = c.NotificationInterval == "off" ? "🔕" : "🔔";
                buttons.Add(new[]
                {
        InlineKeyboardButton.WithCallbackData(
            $"{status} {c.Name} → {GetPrettyInterval(c.NotificationInterval)} 1️⃣",
            $"edit_interval_kwork_{c.CategoryId}")
                });
            }

            foreach (var c in flSubs)
            {
                var status = c.NotificationInterval == "off" ? "🔕" : "🔔";
                buttons.Add(new[]
                {
        InlineKeyboardButton.WithCallbackData(
            $"{status} {c.Name} → {GetPrettyInterval(c.NotificationInterval)} 2️⃣",
            $"edit_interval_fl_{c.CategoryId}")
                });
            }

            foreach (var c in youDo)
            {
                var status = c.NotificationInterval == "off" ? "🔕" : "🔔";
                buttons.Add(new[]
                {
        InlineKeyboardButton.WithCallbackData(
            $"{status} {c.Name} → {GetPrettyInterval(c.NotificationInterval)} 3️⃣",
            $"edit_interval_youdo_{c.CategoryId}")
                });
            }

            foreach (var c in frSubs)
            {
                var status = c.NotificationInterval == "off" ? "🔕" : "🔔";
                buttons.Add(new[]
                {
        InlineKeyboardButton.WithCallbackData(
            $"{status} {c.Name} → {GetPrettyInterval(c.NotificationInterval)} 4️⃣",
            $"edit_interval_fr_{c.CategoryId}")
                });
            }

            foreach (var c in wsSubs)
            {
                var status = c.NotificationInterval == "off" ? "🔕" : "🔔";
                buttons.Add(new[]
                {
        InlineKeyboardButton.WithCallbackData(
            $"{status} {c.Name} → {GetPrettyInterval(c.NotificationInterval)} 5️⃣",
            $"edit_interval_ws_{c.CategorySlug}")
                });
            }

            foreach (var c in profiSubs)
            {
                var status = c.NotificationInterval == "off" ? "🔕" : "🔔";
                buttons.Add(new[]
                {
        InlineKeyboardButton.WithCallbackData(
            $"{status} {c.Name} → {GetPrettyInterval(c.NotificationInterval)} 6️⃣",
            $"edit_interval_profi_{c.Id}")
                });
            }

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "main_menu") });

            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons.ToArray()));
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: new InlineKeyboardMarkup(buttons.ToArray()));
        }

        // ─────────────────────── ОТПРАВКА ЗАКАЗОВ ───────────────────────

        public async Task SendProfiOrderAsync(long chatId, ProfiRuParser.ProfiOrder order, int catId)
        {
            var catName = _db.ProfiCategories.Where(c => c.UserId == chatId).Select(c => c.Name).ToArray();

            var text = $"<b>Profi.ru — {catName[0]}</b>\n\n" +
                       $"<b>{order.Title}</b>\n" +
                       $"Бюджет: <b>{order.Budget}</b>\n" +
                       $"{order.Description}\n\n" +
                       $"<a href=\"{order.Url}\">Перейти к заказу</a>";

            await _bot.SendMessage(chatId, text, ParseMode.Html);
        }

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

        public async Task SetNotificationInterval(long userId, int categoryId, string interval, string platform)
        {
            switch (platform.ToLower())
            {
                case "kwork":
                    var kworkCat = await _db.KworkCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId);
                    if (kworkCat != null)
                    {
                        kworkCat.NotificationInterval = interval;
                        await _db.SaveChangesAsync();
                    }
                    break;

                case "fl":
                    var flCat = await _db.FlCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId);
                    if (flCat != null)
                    {
                        flCat.NotificationInterval = interval;
                        await _db.SaveChangesAsync();
                    }
                    break;

                case "youdo":
                    var youdoCat = await _db.YoudoCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId);
                    if (youdoCat != null)
                    {
                        youdoCat.NotificationInterval = interval;
                        await _db.SaveChangesAsync();
                    }
                    break;
                case "fr":
                    var frCat = await _db.FrCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId);
                    if (frCat != null)
                    {
                        frCat.NotificationInterval = interval;
                        await _db.SaveChangesAsync();
                    }
                    break;
                case "ws":
                    var wrCat = await _db.WorkspaceCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategorySlug == categoryId);
                    if (wrCat != null)
                    {
                        wrCat.NotificationInterval = interval;
                        await _db.SaveChangesAsync();
                    }
                    break;
                case "profi":
                    var profiCat = await _db.ProfiCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == categoryId);
                    if (profiCat != null)
                    {
                        profiCat.NotificationInterval = interval;
                        await _db.SaveChangesAsync();
                    }
                    break;

                default:
                    Console.WriteLine($"Неизвестная платформа: {platform}"); // Лог для дебага
                    break;
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

        public async Task ShowYoudoMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.YoudoCategories.Where(c => c.UserId == userId).ToListAsync();
            var text = "<b>YouDo.com</b>\n\nВыбери категории (статус подписки):";
            var buttons = new List<InlineKeyboardButton[]>();

            foreach (var kvp in YoudoCategories)
            {
                var sub = subs.FirstOrDefault(s => s.CategoryId == kvp.Key);

                string status;
                if (sub == null)
                    status = "❌ OFF";
                else if (sub.NotificationInterval == "off")
                    status = "✅ Выкл";
                else
                {
                    var full = $"✅ {GetPrettyInterval(sub.NotificationInterval)}";
                    status = full.Length > 10 ? full.Substring(0, 8) + ".." : full;
                }

                buttons.Add(new[]
                {
            InlineKeyboardButton.WithCallbackData($"{status} {kvp.Value}", $"youdo_cat_{kvp.Key}")
        });
            }

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "main_menu") });

            var markup = new InlineKeyboardMarkup(buttons.ToArray());
            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: markup);
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: markup);
        }

        public async Task SendYoudoOrderAsync(long chatId, YoudoParser.YoudoOrder order)
        {
            var title = WebUtility.HtmlEncode(order.Title);
            var budgetText = order.Budget != null ? $"\nБюджет: <b>{order.Budget}</b>" : "";
            var addressText = order.Address != null ? $"\nАдрес: <b>{order.Address}</b>" : "";
            var dateText = order.StartDate != null ? $"\nСтарт: <b>{order.StartDate}</b>" : "";
            var desc = order.Description != null ? $"\n\n{WebUtility.HtmlEncode(order.Description[..Math.Min(400, order.Description.Length)])}" : "";

            var text = $"<b>YouDo: {title}</b>{budgetText}{addressText}{dateText}{desc}\n\n<a href=\"{order.Url}\">Перейти к заданию</a>";
            await _bot.SendMessage(chatId, text, ParseMode.Html);
        }

        public async Task ShowIntervalSelection(long chatId, long userId, int categoryId, string platform, int? messageId = null)
        {
            if (platform != "profi")
            {
                var catName = platform.ToLower() switch
                {
                    "kwork" => KworkCategories.GetValueOrDefault(categoryId, "Неизвестная"),
                    "fl" => FlCategories.GetValueOrDefault(categoryId, "Неизвестная"),
                    "youdo" => YoudoCategories.GetValueOrDefault(categoryId, "Неизвестная"),
                    "fr" => FrCategories.GetValueOrDefault(categoryId, "Неизвестная"),
                    "ws" => WsCategories.GetValueOrDefault(categoryId, "Неизвестная"),
                    _ => "Неизвестная категория"
                };

                string currentInterval = "off";
                switch (platform.ToLower())
                {
                    case "kwork":
                        currentInterval = (await _db.KworkCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId))?.NotificationInterval ?? "off";
                        break;
                    case "fl":
                        currentInterval = (await _db.FlCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId))?.NotificationInterval ?? "off";
                        break;
                    case "youdo":
                        currentInterval = (await _db.YoudoCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId))?.NotificationInterval ?? "off";
                        break;
                    case "fr":
                        currentInterval = (await _db.FrCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId))?.NotificationInterval ?? "off";
                        break;
                    case "ws":
                        currentInterval = (await _db.WorkspaceCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategorySlug == categoryId))?.NotificationInterval ?? "off";
                        break;
                }

                var prefix = platform.ToLower();
                var buttons = new[]
                {
        new[] { InlineKeyboardButton.WithCallbackData("Моментально (1 мин)", $"{prefix}_setint_{categoryId}_instant") ,
          InlineKeyboardButton.WithCallbackData("Раз в 5 мин", $"{prefix}_setint_{categoryId}_5min") },
        new[] { InlineKeyboardButton.WithCallbackData("Раз в 15 мин", $"{prefix}_setint_{categoryId}_15min") ,
          InlineKeyboardButton.WithCallbackData("Раз в час", $"{prefix}_setint_{categoryId}_hour") },
        new[] { InlineKeyboardButton.WithCallbackData("Раз в день", $"{prefix}_setint_{categoryId}_day") ,
          InlineKeyboardButton.WithCallbackData("Выключить", $"{prefix}_setint_{categoryId}_off") },
        new[] { InlineKeyboardButton.WithCallbackData("Фильтр по словам", $"set_keywords_{prefix}_{categoryId}") ,
          InlineKeyboardButton.WithCallbackData("Удалить фильтр", $"clear_keywords_{prefix}_{categoryId}") },
        new[] { InlineKeyboardButton.WithCallbackData("Мои подписки", "my_subscriptions"),
          InlineKeyboardButton.WithCallbackData("Назад", $"show_{platform}_categories") },
        new[] { InlineKeyboardButton.WithCallbackData("Удалить подписку", $"delete_{platform}_{categoryId}") }
    };

                var text = $"<b>Настройка уведомлений ({platform.ToUpper()})</b>\n\n" +
                           $"Категория: <b>{catName}</b>\n" +
                           $"Текущий: <b>{GetPrettyInterval(currentInterval)}</b>\n\n" +
                           $"Выбери новый интервал:";

                var markup = new InlineKeyboardMarkup(buttons);
                if (messageId.HasValue)
                {
                    await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: markup);
                }
                else
                {
                    await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: markup);
                }
            }
            else
            {
                var currentInterval = await _db.ProfiCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == categoryId);
                var catName = await _db.ProfiCategories.Where(c => c.Id == categoryId).FirstOrDefaultAsync();
                var prefix = platform.ToLower();
                var buttons = new[]
                {
        new[] { InlineKeyboardButton.WithCallbackData("Моментально (1 мин)", $"{prefix}_setint_{categoryId}_instant") ,
          InlineKeyboardButton.WithCallbackData("Раз в 5 мин", $"{prefix}_setint_{categoryId}_5min") },
        new[] { InlineKeyboardButton.WithCallbackData("Раз в 15 мин", $"{prefix}_setint_{categoryId}_15min") ,
          InlineKeyboardButton.WithCallbackData("Раз в час", $"{prefix}_setint_{categoryId}_hour") },
        new[] { InlineKeyboardButton.WithCallbackData("Раз в день", $"{prefix}_setint_{categoryId}_day") ,
          InlineKeyboardButton.WithCallbackData("Выключить", $"{prefix}_setint_{categoryId}_off") },
        new[] { InlineKeyboardButton.WithCallbackData("Фильтр по словам", $"set_keywords_{prefix}_{categoryId}") ,
          InlineKeyboardButton.WithCallbackData("Удалить фильтр", $"clear_keywords_{prefix}_{categoryId}") },
        new[] { InlineKeyboardButton.WithCallbackData("Мои подписки", "my_subscriptions"),
          InlineKeyboardButton.WithCallbackData("Назад", $"show_{platform}_categories") },
        new[] { InlineKeyboardButton.WithCallbackData("Удалить подписку", $"delete_{platform}_{categoryId}") }
    };

                var text = $"<b>Настройка уведомлений ({platform.ToUpper()})</b>\n\n" +
                           $"Категория: <b>{catName.Name}</b>\n" +
                           $"Текущий: <b>{GetPrettyInterval(currentInterval.NotificationInterval)}</b>\n\n" +
                           $"Выбери новый интервал:";

                var markup = new InlineKeyboardMarkup(buttons);
                if (messageId.HasValue)
                {
                    await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: markup);
                }
                else
                {
                    await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: markup);
                }
            }
        }

        public async Task ShowFrMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.FrCategories.Where(c => c.UserId == userId).ToListAsync();
            var text = "<b>Freelance.ru</b>\n\nВыбери категории (статус подписки):";
            var buttons = new List<InlineKeyboardButton[]>();

            foreach (var kvp in FrCategories)
            {
                var sub = subs.FirstOrDefault(s => s.CategoryId == kvp.Key);

                string status;
                if (sub == null)
                    status = "❌ OFF";
                else if (sub.NotificationInterval == "off")
                    status = "✅ Выкл";
                else
                {
                    var full = $"✅ {GetPrettyInterval(sub.NotificationInterval)}";
                    status = full.Length > 10 ? full.Substring(0, 8) + ".." : full;
                }

                buttons.Add(new[]
                {
            InlineKeyboardButton.WithCallbackData($"{status} {kvp.Value}", $"fr_cat_{kvp.Key}")
        });
            }

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "main_menu") });

            var markup = new InlineKeyboardMarkup(buttons.ToArray());
            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: markup);
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: markup);
        }

        public async Task SendFrOrderAsync(long chatId, FreelanceRuParser.FrOrder order)
        {
            var title = WebUtility.HtmlEncode(order.Title);
            var budget = order.Budget != null ? $"\nБюджет: <b>{order.Budget}</b>" : "";
            var deadline = order.Deadline != null ? $"\nСрок: <b>{order.Deadline}</b>" : "";
            var cat = order.Category != null ? $"\nКатегория: <b>{order.Category}</b>" : "";
            var desc = order.Description != null ? "\n\n" + WebUtility.HtmlEncode(order.Description.Length > 500 ? order.Description[..500] + "…" : order.Description) : "";

            var text = $"<b>Freelance.ru: {title}</b>{budget}{deadline}{cat}{desc}\n\n<a href=\"{order.Url}\">Перейти к проекту</a>";
            await _bot.SendMessage(chatId, text, ParseMode.Html);
        }

        public async Task ShowWorkspaceMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.WorkspaceCategories.Where(c => c.UserId == userId).ToListAsync();
            var text = "<b>Workspace.ru</b>\n\nВыбери категории (статус подписки):";
            var buttons = new List<InlineKeyboardButton[]>();

            foreach (var kvp in WsCategories)
            {
                var sub = subs.FirstOrDefault(s => s.CategorySlug == kvp.Key);

                string status;
                if (sub == null)
                    status = "❌ OFF";
                else if (sub.NotificationInterval == "off")
                    status = "✅ Выкл";
                else
                {
                    var full = $"✅ {GetPrettyInterval(sub.NotificationInterval)}";
                    status = full.Length > 10 ? full.Substring(0, 8) + ".." : full;
                }

                buttons.Add(new[]
                {
            InlineKeyboardButton.WithCallbackData($"{status} {kvp.Value}", $"ws_cat_{kvp.Key}")
        });
            }

            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Назад", "main_menu") });

            var markup = new InlineKeyboardMarkup(buttons.ToArray());
            if (messageId.HasValue)
                await _bot.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: markup);
            else
                await _bot.SendMessage(chatId, text, ParseMode.Html, replyMarkup: markup);
        }

        public async Task SendWsOrderAsync(long chatId, WorkspaceRuParser.WsOrder order)
        {
            var text = $"<b>Workspace.ru</b>\n\n" +
                       $"<b>{order.Title}</b>\n" +
                       $"Бюджет: <b>{order.Budget}</b>\n" +
                       $"Дедлайн: <b>{order.Deadline}</b>\n" +
                       $"Опубликован: {order.Published}\n\n" +
                       $"<a href=\"{order.Url}\">Перейти к тендеру</a>";

            await _bot.SendMessage(chatId, text, ParseMode.Html);
        }

        public async Task EnableMenuButton(long chatId)
        {
            await _bot.SetMyCommands(
                new BotCommand[]
                {
            new() { Command = "/menu", Description = "Главное меню" },
                    // потом добавишь ещё:
                    // new() { Command = "/mysubs", Description = "Мои подписки" },
                    // new() { Command = "/help", Description = "Помощь и поддержка" },
                    // new() { Command = "/stats", Description = "Статистика заказов" },
                },
                new BotCommandScopeChat { ChatId = chatId } // только для этого юзера
            );
        }

        public async Task StartMessage(long chatId)
        {
            var text = $"<b>Привет! Это бот для мониторинга фриланс-бирж.</b>\r\n\n" +
                $"<b>Что ты получаешь:</b>\r\n" +
                $"Мгновенные уведомления о новых заказах (от 1 минуты)\r\n" +
                $"Точные фильтры по словам (битрикс, telegram бот, nuxt, лендинг и любые твои)\r\n" +
                $"Индивидуальные интервалы для каждой категории\r\n\n" +
                $"<b>Как работать:</b>\r\n" +
                $"Нажми на Menu или напиши /menu\r\n" +
                $"Выбери биржу → выбери категории или создай свой поиск\r\n" +
                $"Настрой интервал и фильтр по словам\r\n" +
                $"Готово — заказы приходят автоматически";

            await _bot.SendMessage(chatId, text, ParseMode.Html);
        }
    }
}
