using BotParser.Db;
using BotParser.Models;
using BotParser.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

namespace BotParser
{
    public class BotService : BackgroundService
    {
        private readonly ITelegramBotClient _bot;
        private readonly IServiceProvider _sp;
        private readonly FreelanceService _freelance;
        private readonly KworkBotDbContext _db;
        private readonly ConcurrentDictionary<long, (string platform, int categoryId)> WaitingForKeywords = new();
        private readonly ConcurrentDictionary<long, string> WaitingForProfiCustomQuery = new();
        private readonly ILogger<BotService> _log;

        public BotService(ITelegramBotClient bot, IServiceProvider sp, FreelanceService freelance, KworkBotDbContext db, ILogger<BotService> log)
        {
            _bot = bot;
            _sp = sp;
            _db = db;
            _freelance = freelance;
            _log = log;
        }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _bot.StartReceiving(HandleUpdate, HandleError, cancellationToken: stoppingToken);
        Console.WriteLine("Бот запущен!");
        await Task.Delay(-1, stoppingToken);
    }

    private async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
            if (update.Message?.Text == "/start")
            {
                await _freelance.StartMessage(update.Message.Chat.Id);
                await _freelance.EnableMenuButton(update.Message.Chat.Id);
                await _freelance.EnsureUserExists(update.Message.Chat.Id, update.Message.Chat.Username);
                return;
            }

            if(update.Message?.Text == "/menu")
                await _freelance.ShowMainMenu(update.Message.Chat.Id);

            if (update.Message?.Chat.Id != null)
            {
                var userIds = update.Message!.Chat.Id;

                if (WaitingForProfiCustomQuery.TryGetValue(userIds, out var _))
                {
                    var queryText = update.Message.Text?.Trim();

                    if (string.IsNullOrEmpty(queryText) || queryText.Length < 2)
                    {
                        await bot.SendMessage(userIds, "Ошибка: запрос должен быть от 2 символов.");
                        return;
                    }

                    var newProfi = new ProfiCategory
                    {
                        UserId = userIds,
                        SearchQuery = queryText.ToLower(),
                        Name = queryText.Length > 35 ? queryText.Substring(0, 32) + "..." : queryText,
                        NotificationInterval = "off"
                    };

                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<KworkBotDbContext>();

                    db.ProfiCategories.Add(newProfi);
                    await db.SaveChangesAsync();

                    WaitingForProfiCustomQuery.TryRemove(userIds, out _);

                    await bot.SendMessage(userIds,
                        $"<b>Готово!</b>\n\n" +
                        $"Запрос: <code>{queryText}</code>\n" +
                        $"Интервал: Выключен\n\n",
                        ParseMode.Html,
                        replyMarkup: new InlineKeyboardMarkup(new[]
                        {
            InlineKeyboardButton.WithCallbackData("Мои подписки", "my_subscriptions"),
            InlineKeyboardButton.WithCallbackData("Настроить интервал", $"edit_interval_profi_{newProfi.Id}")
                        }));

                    return;
                }
            }

            if (update.Message?.Chat.Id != null)
            {

                var chatIdm = update.Message!.Chat.Id;

                if (WaitingForKeywords.TryGetValue(chatIdm, out var state))
                {
                    var (platform, catId) = state;

                    var words = update.Message!.Text!
                        .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(w => w.Trim().ToLower())
                        .Where(w => w.Length > 2)
                        .Distinct()
                        .ToList();

                    if (words.Count == 0)
                    {
                        await _bot.SendMessage(chatIdm, "Не нашёл слов. Попробуй ещё раз.");
                        return;
                    }

                    foreach (var word in words)
                    {
                        bool exists = await _db.UserKeywordFilters.AnyAsync(k =>
                            k.UserId == chatIdm &&
                            k.Platform == platform &&
                            k.CategoryId == catId &&
                            k.Word == word);

                        if (!exists)
                        {
                            await _db.UserKeywordFilters.AddAsync(new UserKeywordFilter
                            {
                                UserId = chatIdm,
                                Platform = platform,
                                CategoryId = catId,
                                Word = word
                            });
                        }
                    }

                    await _db.SaveChangesAsync();

                    await _bot.SendMessage(chatIdm, $"Добавлено {words.Count} слов(а) в фильтр!");

                    WaitingForKeywords.TryRemove(chatIdm, out _);
                    return;
                }
            }

            if (update.CallbackQuery is not { } cb) return;

            var data = cb.Data!;
            var userId = cb.From.Id;
            var chatId = cb.Message.Chat.Id;
            var username = cb.From.Username;
            var msgId = cb.Message.MessageId;

            await _freelance.EnsureUserExists(userId, username);

            try
            {
                // Главное меню
                if (data == "main_menu")
                    await _freelance.ShowMainMenu(chatId, msgId);

                else if (data == "kwork_menu")
                    await _freelance.ShowKworkMenu(chatId, userId, msgId);

                else if (data == "fl_menu")
                    await _freelance.ShowFlMenu(chatId, userId, msgId);

                else if (data == "my_subscriptions")
                    await _freelance.ShowMySubscriptions(chatId, userId, msgId);
                
                else if (data == "fr_menu")
                    await _freelance.ShowFrMenu(chatId, userId, msgId);

                else if (data.StartsWith("delete_"))
                {
                    var parts = data["delete_".Length..].Split('_');
                    var platform = parts[0]; // kwork, fl, profi и т.д.
                    var catId = int.Parse(parts[1]);

                    object? subscription = platform switch
                    {
                        "kwork" => await _db.KworkCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId),
                        "fl" => await _db.FlCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId),
                        "youdo" => await _db.YoudoCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId),
                        "fr" => await _db.FrCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId),
                        "ws" => await _db.WorkspaceCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategorySlug == catId),
                        "profi" => await _db.ProfiCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == catId),
                        _ => null
                    };

                    if (subscription != null)
                    {
                        _db.Remove(subscription);
                    }

                    var keywordsToDelete = await _db.UserKeywordFilters
                        .Where(k => k.UserId == userId &&
                                    k.Platform == platform &&
                                    k.CategoryId == catId)
                        .ToListAsync();

                    if (keywordsToDelete.Any())
                    {
                        _db.UserKeywordFilters.RemoveRange(keywordsToDelete);
                        _log.LogInformation("Удалено {Count} ключевых слов для пользователя {UserId}, платформа {Platform}, категория {CatId}",
                            keywordsToDelete.Count, userId, platform, catId);
                    }

                    await _db.SaveChangesAsync();

                    await _bot.AnswerCallbackQuery(cb.Id, "Подписка и фильтр по словам удалены!");

                    await _freelance.ShowMySubscriptions(chatId, userId, msgId);
                }

                else if (data.StartsWith("kwork_cat_"))
                {
                    var catId = int.Parse(data["kwork_cat_".Length..]);
                    var catName = FreelanceService.KworkCategories.GetValueOrDefault(catId, "Неизвестная категория");

                    var existing = await _db.KworkCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId);

                    if (existing != null)
                    {
                        //var keywords = _db.UserKeywordFilters.Where(c => c.CategoryId == catId);
                        //_db.KworkCategories.Remove(existing);
                        //if (keywords != null)
                        //    foreach (var word in keywords) _db.UserKeywordFilters.Remove(word);

                        //await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Вы уже подписаны на данную категорию");
                        //await _freelance.ShowKworkMenu(chatId, userId, msgId);
                    }
                    else
                    {
                        // Новая подписка — создаём с интервалом по умолчанию "instant"
                        await _db.KworkCategories.AddAsync(new KworkCategory
                        {
                            UserId = userId,
                            CategoryId = catId,
                            Name = catName,
                            NotificationInterval = "off" // сразу выключаем
                        });
                        await _db.SaveChangesAsync();

                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка включена! (интервал по умолчанию: off)\nНастрой интервал →");

                        await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "kwork", msgId);
                    }
                }

                else if (data.StartsWith("fl_cat_"))
                {
                    var catId = int.Parse(data["fl_cat_".Length..]);
                    var catName = FreelanceService.FlCategories.GetValueOrDefault(catId, "Неизвестная категория");

                    var existing = await _db.FlCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId);

                    if (existing != null)
                    {
                        //var keywords = _db.UserKeywordFilters.Where(c => c.CategoryId == catId);
                        //_db.FlCategories.Remove(existing);
                        //if (keywords != null)
                        //    foreach (var word in keywords) _db.UserKeywordFilters.Remove(word);

                        //await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Вы уже подписаны на данную категорию");
                        //await _freelance.ShowFlMenu(chatId, userId, msgId);
                    }
                    else
                    {
                        await _db.FlCategories.AddAsync(new FlCategory
                        {
                            UserId = userId,
                            CategoryId = catId,
                            Name = catName,
                            NotificationInterval = "off" //сразу выключаем
                        });
                        await _db.SaveChangesAsync();

                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка включена! (интервал по умолчанию: off)\nНастрой интервал →");
                        await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "fl", msgId);
                    }
                }

                else if (data.StartsWith("edit_interval_kwork_"))
                {
                    var catId = int.Parse(data["edit_interval_kwork_".Length..]);
                    await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "kwork", msgId);
                }

                else if (data.StartsWith("edit_interval_fl_"))
                {
                    var catId = int.Parse(data["edit_interval_fl_".Length..]);
                    await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "fl", msgId);
                }

                else if (data.StartsWith("edit_interval_youdo_"))
                {
                    var catId = int.Parse(data["edit_interval_youdo_".Length..]);
                    await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "youdo", msgId);
                }

                else if (data.StartsWith("edit_interval_fr_"))
                {
                    var catId = int.Parse(data["edit_interval_fr_".Length..]);
                    await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "fr", msgId);
                }
                else if (data.StartsWith("edit_interval_ws_"))
                {
                    var catId = int.Parse(data["edit_interval_ws_".Length..]);
                    await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "ws", msgId);
                }

                else if (data.StartsWith("edit_interval_profi_"))
                {
                    var catId = int.Parse(data["edit_interval_profi_".Length..]);
                    await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "profi", msgId);
                }

                else if (data == "profi_add_custom")
                {
                    WaitingForProfiCustomQuery[chatId] = "true";  // отдельный словарь

                    await _bot.SendMessage(chatId,
                        "Напиши свой поисковый запрос для Profi.ru:\n\n" +
                        "Примеры:\n" +
                        "• <code>битрикс</code>\n" +
                        "• <code>telegram бот python</code>\n" +
                        "• <code>nuxt vue сайт</code>\n" +
                        "• <code>лендинг за 100к</code>",
                        ParseMode.Html);
                }

                else if (data.Contains("_setint_"))
                {
                    var parts = data.Split('_');
                    var platform = parts[0]; // kwork / fl / youdo
                    var catId = int.Parse(parts[2]);
                    var interval = parts[3];

                    await _freelance.SetNotificationInterval(userId, catId, interval, platform);
                    await _bot.AnswerCallbackQuery(cb.Id, $"Интервал: {GetPrettyInterval(interval)}");
                    //await _freelance.ShowMainMenu(chatId);
                }

                else if (data.StartsWith("show_") && data.EndsWith("_categories"))
                {
                    var platform = data["show_".Length..^"_categories".Length]; // kwork, fl, youdo и т.д.

                    switch (platform)
                    {
                        case "kwork":
                            await _freelance.ShowKworkMenu(chatId, userId, msgId);
                            break;
                        case "fl":
                            await _freelance.ShowFlMenu(chatId, userId, msgId);
                            break;
                        case "youdo":
                            await _freelance.ShowYoudoMenu(chatId, userId, msgId);
                            break;
                        case "fr":
                            await _freelance.ShowFrMenu(chatId, userId, msgId);
                            break;
                        case "ws":
                            await _freelance.ShowWorkspaceMenu(chatId, userId, msgId);
                            break;
                        case "profi":
                            await _freelance.ShowProfiMenu(chatId, userId, msgId);
                            break;
                        default:
                            await _freelance.ShowMainMenu(chatId, msgId);
                            break;
                    }
                }

                else if (data == "youdo_menu")
                {

                    await _freelance.ShowYoudoMenu(chatId, userId, msgId);
                }

                else if (data.StartsWith("youdo_cat_"))
                {
                    var catId = int.Parse(data["youdo_cat_".Length..]);
                    var catName = FreelanceService.YoudoCategories.GetValueOrDefault(catId, "Неизвестная");
                    var existing = await _db.YoudoCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId);

                    if (existing != null)
                    {
                        //var keywords = _db.UserKeywordFilters.Where(c => c.CategoryId == catId);
                        //_db.YoudoCategories.Remove(existing);
                        //if (keywords != null)
                        //    foreach (var word in keywords) _db.UserKeywordFilters.Remove(word);

                        //await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Вы уже подписаны на данную категорию");
                        //await _freelance.ShowYoudoMenu(chatId, userId, msgId);
                    }
                    else
                    {
                        await _db.YoudoCategories.AddAsync(new YoudoCategory
                        {
                            UserId = userId,
                            CategoryId = catId,
                            Name = catName,
                            NotificationInterval = "off"  // Дефолт off — без авто-instant
                        });
                        await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка включена! (интервал по умолчанию: off)\nНастрой интервал →");
                        await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "youdo", msgId);
                    }
                }

                else if (data.StartsWith("fr_cat_"))
                {
                    var catId = int.Parse(data["fr_cat_".Length..]);
                    var catName = FreelanceService.FrCategories.GetValueOrDefault(catId, "Неизвестная");
                    var existing = await _db.FrCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId);

                    if (existing != null)
                    {
                        //var keywords = _db.UserKeywordFilters.Where(c => c.CategoryId == catId);
                        //_db.FrCategories.Remove(existing);
                        //if (keywords != null)
                        //    foreach (var word in keywords) _db.UserKeywordFilters.Remove(word);

                        //await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Вы уже подписаны на данную категорию");
                        //await _freelance.ShowFrMenu(chatId, userId, msgId);
                    }
                    else
                    {
                        _db.FrCategories.Add(new FrCategory
                        {
                            UserId = userId,
                            CategoryId = catId,
                            Name = catName,
                            NotificationInterval = "off"
                        });
                        await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка включена! (интервал по умолчанию: off)\nНастрой интервал →");
                    }
                }

                else if (data.StartsWith("profi_cat_"))
                {
                    var catId = int.Parse(data["profi_cat_".Length..]);

                    var existing = await _db.ProfiCategories
                        .FirstOrDefaultAsync(c => c.UserId == userId && c.Id == catId);

                    if (existing != null)
                    {
                        //var keywords = _db.UserKeywordFilters.Where(c => c.CategoryId == catId);
                        //_db.ProfiCategories.Remove(existing);
                        //if (keywords != null)
                        //    foreach (var word in keywords) _db.UserKeywordFilters.Remove(word);

                        //await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Вы уже подписаны на данную категорию");
                        //await _freelance.ShowProfiMenu(chatId, userId, msgId);
                    }
                    else
                    {
                        await _db.ProfiCategories.AddAsync(new ProfiCategory
                        {
                            UserId = userId,
                            SearchQuery = "",
                            Id = catId,
                            Name = existing.Name,
                            NotificationInterval = "off"
                        });
                        await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, $"Подписка включена! (интервал по умолчанию: off)\nНастрой интервал →");
                        await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "profi", msgId);
                    }
                }

                else if (data == "workspace_menu")
                    await _freelance.ShowWorkspaceMenu(chatId, userId, msgId);

                else if (data == "profi_menu")
                    await _freelance.ShowProfiMenu(chatId, userId, msgId);

                else if (data.StartsWith("ws_cat_"))
                {
                    var slug = int.Parse(data["ws_cat_".Length..]);
                    var name = FreelanceService.WsCategories.GetValueOrDefault(slug, "Неизвестно");
                    var existing = await _db.WorkspaceCategories
                        .FirstOrDefaultAsync(c => c.UserId == userId && c.CategorySlug == slug);

                    if (existing != null)
                    {
                        //var keywords = _db.UserKeywordFilters.Where(c => c.CategoryId == slug);
                        //_db.WorkspaceCategories.Remove(existing);
                        //if (keywords != null)
                        //    foreach (var word in keywords) _db.UserKeywordFilters.Remove(word);

                        //await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Вы уже подписаны на данную категорию");
                        //await _freelance.ShowWorkspaceMenu(chatId, userId, msgId);
                    }
                    else
                    {
                        await _db.WorkspaceCategories.AddAsync(new WorkspaceCategory
                        {
                            UserId = userId,
                            CategorySlug = slug,
                            Name = name,
                            NotificationInterval = "off"
                        });
                        await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка включена! (интервал по умолчанию: off)\nНастрой интервал →");
                        await _freelance.ShowIntervalSelection(chatId, userId, slug, platform: "ws", msgId);
                    }
                }

                else if (data.StartsWith("set_keywords_"))
                {
                    // Пример: set_keywords_ws_2 → платформа = "ws", catId = 2
                    var parts = data["set_keywords_".Length..].Split('_'); // ws_2 → ["ws", "2"]
                    var platformShort = parts[0];  // "ws", "fl", "fr", "kwork", "youdo"
                    var catIdStr = parts[1];
                    var catId = int.Parse(catIdStr);

                    // Преобразуем короткое имя в полное (для базы)
                    string platform = platformShort switch
                    {
                        "ws" => "workspace",
                        "fl" => "fl",
                        "fr" => "freelance",
                        "kwork" => "kwork",
                        "youdo" => "youdo",
                        "profi" => "profi",
                        _ => throw new Exception("Неизвестная платформа")
                    };

                    if (platform != "profi")
                    {

                        // Получаем название рубрики
                        string categoryName = platform switch
                        {
                            "workspace" => FreelanceService.WsCategories.GetValueOrDefault(catId, "Неизвестно"),
                            "fl" => FreelanceService.FlCategories.GetValueOrDefault(catId, "Неизвестно"),
                            "freelance" => FreelanceService.FrCategories.GetValueOrDefault(catId, "Неизвестно"),
                            "kwork" => FreelanceService.KworkCategories.GetValueOrDefault(catId, "Неизвестно"),
                            "youdo" => FreelanceService.YoudoCategories.GetValueOrDefault(catId, "Неизвестно"),
                            _ => "Неизвестно"
                        };

                        // Сохраняем состояние
                        WaitingForKeywords[chatId] = (platform, catId);

                        // Красивое сообщение
                        await _bot.SendMessage(chatId,
                            $"<b>Фильтр по словам</b>\n\n" +
                            $"Платформа: <b>{GetPlatformName(platformShort)}</b>\n" +
                            $"Рубрика: <b>{categoryName}</b>\n\n" +
                            $"Отправь слова через запятую:\n" +
                            $"Например: битрикс, laravel, под ключ, telegram bot",
                            ParseMode.Html);
                    }
                    else
                    {
                        var categoryName = await _db.ProfiCategories.Where(c => c.Id == catId).FirstOrDefaultAsync();

                        // Сохраняем состояние
                        WaitingForKeywords[chatId] = (platform, catId);

                        // Красивое сообщение
                        await _bot.SendMessage(chatId,
                            $"<b>Фильтр по словам</b>\n\n" +
                            $"Платформа: <b>{GetPlatformName(platformShort)}</b>\n" +
                            $"Рубрика: <b>{categoryName.Name}</b>\n\n" +
                            $"Отправь слова через запятую:\n" +
                            $"Например: битрикс, laravel, под ключ, telegram bot",
                            ParseMode.Html);
                    }
                }

                else if (data?.StartsWith("clear_keywords_") == true)
                {
                    var parts = data["clear_keywords_".Length..].Split('_');
                    var platformShort = parts[0]; // ws, fl, fr, kwork, youdo
                    var catId = int.Parse(parts[1]);

                    // Преобразуем короткое имя в полное (как в set_keywords)
                    string platform = platformShort switch
                    {
                        "ws" => "workspace",
                        "fl" => "fl",
                        "fr" => "freelance",
                        "kwork" => "kwork",
                        "youdo" => "youdo",
                        "profi" => "profi",
                        _ => throw new Exception("Неизвестная платформа")
                    };

                    if (platform != "profi")
                    {
                        // Получаем название рубрики для красивого ответа
                        string categoryName = platform switch
                        {
                            "workspace" => FreelanceService.WsCategories.GetValueOrDefault(catId, "Неизвестно"),
                            "fl" => FreelanceService.FlCategories.GetValueOrDefault(catId, "Неизвестно"),
                            "freelance" => FreelanceService.FrCategories.GetValueOrDefault(catId, "Неизвестно"),
                            "kwork" => FreelanceService.KworkCategories.GetValueOrDefault(catId, "Неизвестно"),
                            "youdo" => FreelanceService.YoudoCategories.GetValueOrDefault(catId, "Неизвестно"),
                            _ => "Неизвестно"
                        };

                        // УДАЛЯЕМ ВСЕ слова для этой рубрики у этого пользователя
                        var keywordsToDelete = await _db.UserKeywordFilters
                            .Where(k => k.UserId == userId && k.Platform == platform && k.CategoryId == catId)
                            .ToListAsync();

                        if (keywordsToDelete.Any())
                        {
                            _db.UserKeywordFilters.RemoveRange(keywordsToDelete);
                            await _db.SaveChangesAsync();

                            await _bot.AnswerCallbackQuery(cb.Id, "Фильтр удалён!");
                            await _bot.SendMessage(chatId,
                                $"<b>Фильтр по словам удалён</b>\n\n" +
                                $"Платформа: <b>{GetPlatformName(platformShort)}</b>\n" +
                                $"Рубрика: <b>{categoryName}</b>\n\n" +
                                $"Теперь будут приходить все заказы из этой рубрики.",
                                ParseMode.Html);
                        }
                        else
                        {
                            await _bot.AnswerCallbackQuery(cb.Id, "Фильтр и так пустой");
                        }
                    }
                    else
                    {
                        // Получаем название рубрики для красивого ответа
                        var categoryName = _db.ProfiCategories.Where(c => c.Id == catId).FirstOrDefault();

                        // УДАЛЯЕМ ВСЕ слова для этой рубрики у этого пользователя
                        var keywordsToDelete = await _db.UserKeywordFilters
                            .Where(k => k.UserId == userId && k.Platform == platform && k.CategoryId == catId)
                            .ToListAsync();

                        if (keywordsToDelete.Any())
                        {
                            _db.UserKeywordFilters.RemoveRange(keywordsToDelete);
                            await _db.SaveChangesAsync();

                            await _bot.AnswerCallbackQuery(cb.Id, "Фильтр удалён!");
                            await _bot.SendMessage(chatId,
                                $"<b>Фильтр по словам удалён</b>\n\n" +
                                $"Платформа: <b>{GetPlatformName(platformShort)}</b>\n" +
                                $"Рубрика: <b>{categoryName.Name}</b>\n\n" +
                                $"Теперь будут приходить все заказы из этой рубрики.",
                                ParseMode.Html);
                        }
                        else
                        {
                            await _bot.AnswerCallbackQuery(cb.Id, "Фильтр и так пустой");
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "ОШИБКА В CALLBACK | " +
                    "Пользователь: {UserId} ({Username}) | " +
                    "ChatId: {ChatId} | " +
                    "CallbackData: {CallbackData} | " +
                    "Сообщение ID: {MessageId} | " +
                    "Время: {Time}",
                    userId,
                    username ?? "без username",
                    chatId,
                    data ?? "null",
                    msgId,
                    DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));

                try
                {
                    await _bot.AnswerCallbackQuery(cb.Id, "Произошла ошибка, попробуй ещё раз");
                }
                catch { /* игнорируем, если не удалось */ }
            }
        }

        private static string GetPlatformName(string shortName) => shortName switch
        {
            "ws" => "Workspace.ru",
            "fl" => "FL.ru",
            "fr" => "Freelance.ru",
            "kwork" => "Kwork.ru",
            "youdo" => "YouDo.com",
            "profi" => "Profi.ru",
            _ => shortName
        };

        private string GetPrettyInterval(string interval) => interval switch
        {
            "instant" => "Моментально",
            "5min" => "Раз в 5 минут",
            "15min" => "Раз в 15 минут",
            "hour" => "Раз в час",
            "day" => "Раз в день",
            "off" => "Выключено",
            _ => interval
        };

        private Task HandleError(ITelegramBotClient bot, Exception ex, CancellationToken ct)
        {
        Console.WriteLine($"Ошибка бота: {ex}");
        return Task.CompletedTask;
        }
    }
}
