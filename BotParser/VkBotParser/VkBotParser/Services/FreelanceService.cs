using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;
using VkBotParser.Db;
using VkBotParser.Parsers;
using VkNet;
using VkNet.Enums.StringEnums;
using VkNet.Model;

namespace VkBotParser.Services
{
    public class FreelanceService
    {
        private readonly VkApi _bot;
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

        public FreelanceService(VkApi bot, KworkBotDbContext db, KworkParser kworkParser, FlParser flParser)
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

        private async Task SendMessage(long userId, string text, MessageKeyboard keyboard = null)
        {
            await _bot.Messages.SendAsync(new MessagesSendParams
            {
                UserId = userId,
                Message = text,
                Keyboard = keyboard,
                RandomId = Random.Shared.Next()
            });
        }

        // ─────────────────────── ГЛАВНОЕ МЕНЮ ───────────────────────

        public async Task ShowMainMenu(long chatId, int? messageId = null)
        {
            var text = "Главное меню:";

            var keyboard = new KeyboardBuilder()
                .AddButton("Kwork.ru", "kwork_menu", KeyboardButtonColor.Primary)
                .AddButton("FL.ru", "fl_menu", KeyboardButtonColor.Primary)
                .AddLine()
                .AddButton("YouDo.com", "youdo_menu", KeyboardButtonColor.Primary)
                .AddButton("Freelance.ru", "fr_menu", KeyboardButtonColor.Primary)
                .AddLine()
                .AddButton("Workspace.ru", "workspace_menu", KeyboardButtonColor.Primary)
                .AddButton("Profi.ru", "profi_menu", KeyboardButtonColor.Primary)
                .AddLine()
                .AddButton("Мои подписки", "my_subscriptions", KeyboardButtonColor.Secondary)
                .Build();

            await SendMessage(chatId, text, keyboard);
        }

        // ─────────────────────── МЕНЮ PROFI ───────────────────────
        public async Task ShowProfiMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.ProfiCategories.Where(c => c.UserId == userId).ToListAsync();
            var text = "<b>Profi.ru — персональный поиск</b>\n\n" +
                       "Ты сам создаёшь запросы — получаешь только нужные заказы.\n\n" +
                       "Примеры:\n" +
                       "• битрикс\n" +
                       "• telegram бот\n" +
                       "• nuxt vue сайт\n" +
                       "• лендинг за 100к";

            var keyboard = new KeyboardBuilder()
                .AddButton("Добавить свой поиск", "profi_add_custom", KeyboardButtonColor.Positive)
                .AddButton("Назад", "main_menu", KeyboardButtonColor.Negative)
                .Build();

            await SendMessage(userId, text, keyboard);
        }

        // ─────────────────────── МЕНЮ KWORK ───────────────────────
        public async Task ShowKworkMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.KworkCategories.Where(c => c.UserId == userId).ToListAsync();
            var text = "<b>Kwork.ru</b>\n\nВыбери категории (статус подписки):";

            var keyboard = new KeyboardBuilder();

            foreach (var kvp in KworkCategories)
            {
                var sub = subs.FirstOrDefault(s => s.CategoryId == kvp.Key);
                string status = sub == null ? "❌ OFF" : (sub.NotificationInterval == "off" ? "✅ Выкл" : $"✅ {GetPrettyInterval(sub.NotificationInterval)}");
                keyboard.AddButton($"{status} {kvp.Value}", $"kwork_cat_{kvp.Key}", KeyboardButtonColor.Primary);
                keyboard.AddLine();
            }

            keyboard.AddButton("Назад", "main_menu", KeyboardButtonColor.Negative);

            await SendMessage(userId, text, keyboard.Build());
        }

        // ─────────────────────── МЕНЮ FL.RU ───────────────────────
        public async Task ShowFlMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.FlCategories.Where(c => c.UserId == userId).ToListAsync();
            var text = "<b>FL.ru</b>\n\nВыбери категории (статус подписки):";

            var keyboard = new KeyboardBuilder();

            foreach (var kvp in FlCategories)
            {
                var sub = subs.FirstOrDefault(s => s.CategoryId == kvp.Key);
                string status = sub == null ? "❌ OFF" : (sub.NotificationInterval == "off" ? "✅ Выкл" : $"✅ {GetPrettyInterval(sub.NotificationInterval)}");
                keyboard.AddButton($"{status} {kvp.Value}", $"fl_cat_{kvp.Key}", KeyboardButtonColor.Primary);
                keyboard.AddLine();
            }

            keyboard.AddButton("Назад", "main_menu", KeyboardButtonColor.Negative);

            await SendMessage(userId, text, keyboard.Build());
        }

        // ─────────────────────── МОИ ПОДПИСКИ ───────────────────────
        public async Task ShowMySubscriptions(long chatId, long userId, int? messageId = null)
        {
            var kwork = await _db.KworkCategories.AnyAsync(c => c.UserId == userId);
            var fl = await _db.FlCategories.AnyAsync(c => c.UserId == userId);
            var youdo = await _db.YoudoCategories.AnyAsync(c => c.UserId == userId);
            var fr = await _db.FrCategories.AnyAsync(c => c.UserId == userId);
            var ws = await _db.WorkspaceCategories.AnyAsync(c => c.UserId == userId);
            var profi = await _db.ProfiCategories.AnyAsync(c => c.UserId == userId);

            if (!kwork && !fl && !youdo && !fr && !ws && !profi)
            {
                var keyboards = new KeyboardBuilder()
                    .AddButton("Назад", "main_menu", KeyboardButtonColor.Secondary)
                    .Build();

                await SendMessage(userId, "У тебя пока нет активных подписок 😔", keyboards);
                return;
            }

            var text = "<b>Мои подписки</b>\n\nВыбери биржу:";

            var keyboard = new KeyboardBuilder();

            if (kwork) keyboard.AddButton("Kwork.ru 1️⃣", "my_subs_kwork", KeyboardButtonColor.Primary);
            if (fl) keyboard.AddButton("FL.ru 2️⃣", "my_subs_fl", KeyboardButtonColor.Primary);
            if (youdo) keyboard.AddButton("YouDo 3️⃣", "my_subs_youdo", KeyboardButtonColor.Primary);
            if (fr) keyboard.AddButton("Freelance.ru 4️⃣", "my_subs_fr", KeyboardButtonColor.Primary);
            if (ws) keyboard.AddButton("Workspace.ru 5️⃣", "my_subs_ws", KeyboardButtonColor.Primary);
            if (profi) keyboard.AddButton("Profi.ru 6️⃣", "my_subs_profi", KeyboardButtonColor.Primary);

            keyboard.AddLine().AddButton("Назад", "main_menu", KeyboardButtonColor.Secondary);

            await SendMessage(userId, text, keyboard.Build());
        }

        public async Task ShowMySubscriptionsByPlatform(long chatId, long userId, string platform, int? messageId = null)
        {
            List<object> subs = platform.ToLower() switch
            {
                "kwork" => (await _db.KworkCategories.Where(c => c.UserId == userId).ToListAsync()).Cast<object>().ToList(),
                "fl" => (await _db.FlCategories.Where(c => c.UserId == userId).ToListAsync()).Cast<object>().ToList(),
                "youdo" => (await _db.YoudoCategories.Where(c => c.UserId == userId).ToListAsync()).Cast<object>().ToList(),
                "fr" => (await _db.FrCategories.Where(c => c.UserId == userId).ToListAsync()).Cast<object>().ToList(),
                "ws" => (await _db.WorkspaceCategories.Where(c => c.UserId == userId).ToListAsync()).Cast<object>().ToList(),
                "profi" => (await _db.ProfiCategories.Where(c => c.UserId == userId).ToListAsync()).Cast<object>().ToList(),
                _ => new List<object>()
            };

            if (!subs.Any())
            {
                var keyboards = new KeyboardBuilder()
                    .AddButton("Назад", "my_subscriptions", KeyboardButtonColor.Secondary)
                    .Build();

                await SendMessage(userId, "Подписки на этой бирже отсутствуют", keyboards);
                return;
            }

            var platformName = GetPlatformName(platform);
            var text = $"<b>{platformName}</b>\n\nАктивные подписки:";

            var keyboard = new KeyboardBuilder();

            foreach (dynamic c in subs)
            {
                string name = c.Name ?? "Без названия";
                string interval = c.NotificationInterval ?? "off";
                var status = interval == "off" ? "🔕" : "🔔";
                string id = c.CategoryId?.ToString() ?? c.Id?.ToString() ?? c.CategorySlug?.ToString();

                keyboard.AddButton($"{status} {name} → {GetPrettyInterval(interval)}", $"edit_interval_{platform}_{id}", KeyboardButtonColor.Primary);
                keyboard.AddLine();
            }

            keyboard.AddButton("Назад к биржам", "my_subscriptions", KeyboardButtonColor.Secondary);

            await SendMessage(userId, text, keyboard.Build());
        }

        // ─────────────────────── ОТПРАВКА ЗАКАЗОВ ───────────────────────

        public async Task SendProfiOrderAsync(long chatId, ProfiRuParser.ProfiOrder order, int catId)
        {
            try
            {
                var catName = _db.ProfiCategories.Where(c => c.UserId == chatId).Select(c => c.Name).ToArray();

                var text = $"<b>Profi.ru — {catName[0]}</b>\n\n" +
                           $"<b>{order.Title}</b>\n" +
                           $"Бюджет: <b>{order.Budget}</b>\n" +
                           $"{order.Description}\n\n" +
                           $"<a href=\"{order.Url}\">Перейти к заказу</a>";

                await SendMessage(chatId, text);
            }
            //catch (ApiRequestException ex) when (ex.ErrorCode == 403 && ex.Message.Contains("blocked"))
            //{

            //    await RemoveUserCompletelyAsync(chatId);
            //}
            catch (Exception ex)
            {

            }
        }

        public async Task SendKworkOrderAsync(long chatId, KworkParser.KworkOrder order)
        {
            try
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

                await SendMessage(chatId, text);
            }
            //catch (ApiRequestException ex) when (ex.ErrorCode == 403 && ex.Message.Contains("blocked"))
            //{

            //    await RemoveUserCompletelyAsync(chatId);
            //}
            catch (Exception ex)
            {

            }
        }

        public async Task SendFlOrderAsync(long chatId, FlParser.FlOrder order)
        {
            try
            {
                var title = WebUtility.HtmlEncode(order.Title);
                var budgetText = order.Budget != null ? $"\nБюджет: <b>{order.Budget}</b>" : "";
                var desc = string.IsNullOrWhiteSpace(order.Description)
                    ? "" : WebUtility.HtmlEncode(order.Description.Length > 400 ? order.Description[..400] + "…" : order.Description);

                var text = $"<b>FL.ru: {title}</b>{budgetText}\n\n{desc}\n\n<a href=\"{order.Url}\">{order.Url}</a>";

                await SendMessage(chatId, text);
            }
            //catch (ApiRequestException ex) when (ex.ErrorCode == 403 && ex.Message.Contains("blocked"))
            //{

            //    await RemoveUserCompletelyAsync(chatId);
            //}
            catch (Exception ex)
            {

            }
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
                    CreatedAt = DateTime.UtcNow,
                    ProfiEncryptedPassword = null,
                    ProfiLogin = null
                });
                await _db.SaveChangesAsync();
            }
        }

        public async Task ShowYoudoMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.YoudoCategories.Where(c => c.UserId == userId).ToListAsync();
            var text = "<b>Youdo.ru</b>\n\nВыбери категории (статус подписки):";

            var keyboard = new KeyboardBuilder();

            foreach (var kvp in YoudoCategories)
            {
                var sub = subs.FirstOrDefault(s => s.CategoryId == kvp.Key);
                string status = sub == null ? "❌ OFF" : (sub.NotificationInterval == "off" ? "✅ Выкл" : $"✅ {GetPrettyInterval(sub.NotificationInterval)}");
                keyboard.AddButton($"{status} {kvp.Value}", $"youdo_cat_{kvp.Key}", KeyboardButtonColor.Primary);
                keyboard.AddLine();
            }

            keyboard.AddButton("Назад", "main_menu", KeyboardButtonColor.Negative);

            await SendMessage(userId, text, keyboard.Build());
        }

        public async Task SendYoudoOrderAsync(long chatId, YoudoParser.YoudoOrder order)
        {
            try
            {
                var title = WebUtility.HtmlEncode(order.Title);
                var budgetText = order.Budget != null ? $"\nБюджет: <b>{order.Budget}</b>" : "";
                var addressText = order.Address != null ? $"\nАдрес: <b>{order.Address}</b>" : "";
                var dateText = order.StartDate != null ? $"\nСтарт: <b>{order.StartDate}</b>" : "";
                var desc = order.Description != null ? $"\n\n{WebUtility.HtmlEncode(order.Description[..Math.Min(400, order.Description.Length)])}" : "";

                var text = $"<b>YouDo: {title}</b>{budgetText}{addressText}{dateText}{desc}\n\n<a href=\"{order.Url}\">Перейти к заданию</a>";

                await SendMessage(chatId, text);
            }
            //catch (ApiRequestException ex) when (ex.ErrorCode == 403 && ex.Message.Contains("blocked"))
            //{

            //    await RemoveUserCompletelyAsync(chatId);
            //}
            catch (Exception ex)
            {

            }
        }

        public async Task ShowIntervalSelection(long chatId, long userId, int categoryId, string platform, int? messageId = null)
        {
            string catName = "Неизвестная категория";
            string currentInterval = "off";

            if (platform != "profi")
            {
                catName = platform.ToLower() switch
                {
                    "kwork" => KworkCategories.GetValueOrDefault(categoryId, "Неизвестная"),
                    "fl" => FlCategories.GetValueOrDefault(categoryId, "Неизвестная"),
                    "youdo" => YoudoCategories.GetValueOrDefault(categoryId, "Неизвестная"),
                    "fr" => FrCategories.GetValueOrDefault(categoryId, "Неизвестная"),
                    "ws" => WsCategories.GetValueOrDefault(categoryId, "Неизвестная"),
                    _ => "Неизвестная категория"
                };

                currentInterval = platform.ToLower() switch
                {
                    "kwork" => (await _db.KworkCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId))?.NotificationInterval ?? "off",
                    "fl" => (await _db.FlCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId))?.NotificationInterval ?? "off",
                    "youdo" => (await _db.YoudoCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId))?.NotificationInterval ?? "off",
                    "fr" => (await _db.FrCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == categoryId))?.NotificationInterval ?? "off",
                    "ws" => (await _db.WorkspaceCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategorySlug == categoryId))?.NotificationInterval ?? "off",
                    _ => "off"
                };
            }
            else
            {
                var profiCat = await _db.ProfiCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == categoryId);
                catName = profiCat?.Name ?? "Неизвестная";
                currentInterval = profiCat?.NotificationInterval ?? "off";
            }

            var prefix = platform.ToLower();

            var keyboard = new KeyboardBuilder()
                .AddButton("Моментально (1 мин)", $"{prefix}_setint_{categoryId}_instant", KeyboardButtonColor.Primary)
                .AddButton("Раз в 5 мин", $"{prefix}_setint_{categoryId}_5min", KeyboardButtonColor.Primary)
                .AddLine()
                .AddButton("Раз в 15 мин", $"{prefix}_setint_{categoryId}_15min", KeyboardButtonColor.Primary)
                .AddButton("Раз в час", $"{prefix}_setint_{categoryId}_hour", KeyboardButtonColor.Primary)
                .AddLine()
                .AddButton("Раз в день", $"{prefix}_setint_{categoryId}_day", KeyboardButtonColor.Primary)
                .AddButton("Выключить", $"{prefix}_setint_{categoryId}_off", KeyboardButtonColor.Negative)
                .AddLine()
                .AddButton("Фильтр по словам", $"set_keywords_{prefix}_{categoryId}", KeyboardButtonColor.Secondary)
                .AddButton("Удалить фильтр", $"clear_keywords_{prefix}_{categoryId}", KeyboardButtonColor.Secondary)
                .AddLine()
                .AddButton("Мои подписки", "my_subscriptions", KeyboardButtonColor.Secondary)
                .AddButton("Назад к категориям", $"show_{platform}_categories", KeyboardButtonColor.Secondary)
                .AddLine()
                .AddButton("Удалить подписку", $"delete_{platform}_{categoryId}", KeyboardButtonColor.Negative)
                .Build();

            var text = $"<b>Настройка уведомлений ({platform.ToUpper()})</b>\n\n" +
                       $"Категория: <b>{catName}</b>\n" +
                       $"Текущий: <b>{GetPrettyInterval(currentInterval)}</b>\n\n" +
                       $"Выбери новый интервал:";

            await SendMessage(userId, text, keyboard);
        }

        public async Task ShowFrMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.FrCategories.Where(c => c.UserId == userId).ToListAsync();
            var text = "<b>Freelance.ru</b>\n\nВыбери категории (статус подписки):";

            var keyboard = new KeyboardBuilder();

            foreach (var kvp in FrCategories)
            {
                var sub = subs.FirstOrDefault(s => s.CategoryId == kvp.Key);
                string status = sub == null ? "❌ OFF" : (sub.NotificationInterval == "off" ? "✅ Выкл" : $"✅ {GetPrettyInterval(sub.NotificationInterval)}");
                keyboard.AddButton($"{status} {kvp.Value}", $"fr_cat_{kvp.Key}", KeyboardButtonColor.Primary);
                keyboard.AddLine();
            }

            keyboard.AddButton("Назад", "main_menu", KeyboardButtonColor.Negative);

            await SendMessage(userId, text, keyboard.Build());
        }

        public async Task SendFrOrderAsync(long chatId, FreelanceRuParser.FrOrder order)
        {
            try
            {
                var title = WebUtility.HtmlEncode(order.Title);
                var budget = order.Budget != null ? $"\nБюджет: <b>{order.Budget}</b>" : "";
                var deadline = order.Deadline != null ? $"\nСрок: <b>{order.Deadline}</b>" : "";
                var cat = order.Category != null ? $"\nКатегория: <b>{order.Category}</b>" : "";
                var desc = order.Description != null ? "\n\n" + WebUtility.HtmlEncode(order.Description.Length > 500 ? order.Description[..500] + "…" : order.Description) : "";

                var text = $"<b>Freelance.ru: {title}</b>{budget}{deadline}{cat}{desc}\n\n<a href=\"{order.Url}\">Перейти к проекту</a>";
                await SendMessage(chatId, text);
            }
            //catch (ApiRequestException ex) when (ex.ErrorCode == 403 && ex.Message.Contains("blocked"))
            //{

            //    await RemoveUserCompletelyAsync(chatId);
            //}
            catch (Exception ex)
            {

            }
        }

        public async Task ShowWorkspaceMenu(long chatId, long userId, int? messageId = null)
        {
            var subs = await _db.WorkspaceCategories.Where(c => c.UserId == userId).ToListAsync();
            var text = "<b>Workspace.ru</b>\n\nВыбери категории (статус подписки):";

            var keyboard = new KeyboardBuilder();

            foreach (var kvp in WsCategories)
            {
                var sub = subs.FirstOrDefault(s => s.CategorySlug == kvp.Key);
                string status = sub == null ? "❌ OFF" : (sub.NotificationInterval == "off" ? "✅ Выкл" : $"✅ {GetPrettyInterval(sub.NotificationInterval)}");
                keyboard.AddButton($"{status} {kvp.Value}", $"ws_cat_{kvp.Key}", KeyboardButtonColor.Primary);
                keyboard.AddLine();
            }

            keyboard.AddButton("Назад", "main_menu", KeyboardButtonColor.Negative);

            await SendMessage(userId, text, keyboard.Build());
        }

        public async Task SendWsOrderAsync(long chatId, WorkspaceRuParser.WsOrder order)
        {
            try
            {
                var text = $"<b>Workspace.ru</b>\n\n" +
                           $"<b>{order.Title}</b>\n" +
                           $"Бюджет: <b>{order.Budget}</b>\n" +
                           $"Дедлайн: <b>{order.Deadline}</b>\n" +
                           $"Опубликован: {order.Published}\n\n" +
                           $"<a href=\"{order.Url}\">Перейти к тендеру</a>";

                await SendMessage(chatId, text);
            }
            //catch (ApiRequestException ex) when (ex.ErrorCode == 403 && ex.Message.Contains("blocked"))
            //{

            //    await RemoveUserCompletelyAsync(chatId);
            //}
            catch (Exception ex)
            {

            }
        }

        private async Task RemoveUserCompletelyAsync(long userId)
        {
            // Удаляем все подписки
            var kwork = _db.KworkCategories.Where(c => c.UserId == userId);
            _db.KworkCategories.RemoveRange(kwork);

            var fl = _db.FlCategories.Where(c => c.UserId == userId);
            _db.FlCategories.RemoveRange(fl);

            var youdo = _db.YoudoCategories.Where(c => c.UserId == userId);
            _db.YoudoCategories.RemoveRange(youdo);

            var fr = _db.FrCategories.Where(c => c.UserId == userId);
            _db.FrCategories.RemoveRange(fr);

            var ws = _db.WorkspaceCategories.Where(c => c.UserId == userId);
            _db.WorkspaceCategories.RemoveRange(ws);

            var profi = _db.ProfiCategories.Where(c => c.UserId == userId);
            _db.ProfiCategories.RemoveRange(profi);

            // Удаляем фильтры по словам
            var keywords = _db.UserKeywordFilters.Where(k => k.UserId == userId);
            _db.UserKeywordFilters.RemoveRange(keywords);

            // Удаляем отправленные заказы (чтобы не накапливать)
            _db.SentOrders.RemoveRange(_db.SentOrders.Where(s => s.UserTelegramId == userId));
            _db.SentFlOrders.RemoveRange(_db.SentFlOrders.Where(s => s.UserTelegramId == userId));
            _db.SentYoudoOrders.RemoveRange(_db.SentYoudoOrders.Where(s => s.UserTelegramId == userId));
            _db.SentFrOrders.RemoveRange(_db.SentFrOrders.Where(s => s.UserTelegramId == userId));
            _db.SentWsOrders.RemoveRange(_db.SentWsOrders.Where(s => s.UserTelegramId == userId));
            _db.SentProfiOrders.RemoveRange(_db.SentProfiOrders.Where(s => s.UserTelegramId == userId));

            // Удаляем самого пользователя
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
                _db.Users.Remove(user);

            await _db.SaveChangesAsync();
        }

        //public async Task EnableMenuButton(long chatId)
        //{
        //    await _bot.SetMyCommands(
        //        new BotCommand[]
        //        {
        //    new() { Command = "/menu", Description = "Главное меню" },
        //            // потом добавишь ещё:
        //            // new() { Command = "/mysubs", Description = "Мои подписки" },
        //            // new() { Command = "/help", Description = "Помощь и поддержка" },
        //            // new() { Command = "/stats", Description = "Статистика заказов" },
        //        },
        //        new BotCommandScopeChat { ChatId = chatId } // только для этого юзера
        //    );
        //}

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

            await SendMessage(chatId, text);
        }

        //private readonly ReplyKeyboardMarkup _persistentMenu = new(new[]
        //{
        //    new[]
        //    {
        //        new KeyboardButton("📚 Главное меню"),
        //        new KeyboardButton("⭐ Мои подписки")
        //    }
        //})
        //{
        //    ResizeKeyboard = true // кнопки под размер экрана
        //};

        private string GetPlatformName(string platform) => platform.ToLower() switch
        {
            "kwork" => "Kwork.ru",
            "fl" => "FL.ru",
            "youdo" => "YouDo",
            "fr" => "Freelance.ru",
            "ws" => "Workspace.ru",
            "profi" => "Profi.ru",
            _ => platform
        };
    }
}
