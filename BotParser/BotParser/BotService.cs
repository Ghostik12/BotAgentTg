using BotParser.Db;
using BotParser.Models;
using BotParser.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace BotParser
{
    public class BotService : BackgroundService
    {
    private readonly ITelegramBotClient _bot;
    private readonly IServiceProvider _sp;
    private readonly FreelanceService _freelance;
    private readonly KworkBotDbContext _db;

    public BotService(ITelegramBotClient bot, IServiceProvider sp, FreelanceService freelance, KworkBotDbContext db)
    {
        _bot = bot;
        _sp = sp;
        _db = db;
        _freelance = freelance;
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
                await _freelance.ShowMainMenu(update.Message.Chat.Id);
                await _freelance.EnsureUserExists(update.Message.Chat.Id, update.Message.Chat.Username);
                return;
        }

        if (update.CallbackQuery is not { } cb) return;

        var data = cb.Data!;
        var chatId = cb.Message!.Chat.Id;
        var userId = cb.From.Id;
        var username = cb.From.Username;
        var msgId = cb.Message.MessageId;
        var mId = cb.Id;

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

                else if (data.StartsWith("kwork_cat_"))
                {
                    var catId = int.Parse(data["kwork_cat_".Length..]);
                    var catName = FreelanceService.KworkCategories.GetValueOrDefault(catId, "Неизвестная категория");

                    var existing = await _db.KworkCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId);

                    if (existing != null)
                    {
                        // Была подписка — отключаем
                        _db.KworkCategories.Remove(existing);
                        await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка отключена");
                        await _freelance.ShowKworkMenu(chatId, userId, msgId);
                    }
                    else
                    {
                        // Новая подписка — создаём с интервалом по умолчанию "instant"
                        _db.KworkCategories.Add(new KworkCategory
                        {
                            UserId = userId,
                            CategoryId = catId,
                            Name = catName,
                            NotificationInterval = "off" // сразу выключаем
                        });
                        await _db.SaveChangesAsync();

                        await _bot.AnswerCallbackQuery(cb.Id, "ППодписка включена! (интервал по умолчанию: off)\nНастрой интервал →");

                        // ←←←← СРАЗУ ПРОВАЛИВАЕМСЯ В ВЫБОР ИНТЕРВАЛА
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
                        _db.FlCategories.Remove(existing);
                        await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка отключена");
                        await _freelance.ShowFlMenu(chatId, userId, msgId);
                    }
                    else
                    {
                        _db.FlCategories.Add(new FlCategory
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
                    await _freelance.ShowIntervalSelection(chatId, userId, catId, platform:"kwork", msgId);
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

                else if (data == "youdo_menu")
                    await _freelance.ShowYoudoMenu(chatId, userId, msgId);

                else if (data.StartsWith("youdo_cat_"))
                {
                    var catId = int.Parse(data["youdo_cat_".Length..]);
                    var catName = FreelanceService.YoudoCategories.GetValueOrDefault(catId, "Неизвестная");
                    var existing = await _db.YoudoCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId);

                    if (existing != null)
                    {
                        _db.YoudoCategories.Remove(existing);
                        await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка отключена");
                    }
                    else
                    {
                        _db.YoudoCategories.Add(new YoudoCategory
                        {
                            UserId = userId,
                            CategoryId = catId,
                            Name = catName,
                            NotificationInterval = "off"  // Дефолт off — без авто-instant
                        });
                        await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка включена! (интервал по умолчанию: off)\nНастрой интервал →");
                    }

                    // ← Только обновляем меню YouDo, без прыжка в интервалы
                    await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "youdo", msgId);
                }

                else if (data.StartsWith("fr_cat_"))
                {
                    var catId = int.Parse(data["fr_cat_".Length..]);
                    var catName = FreelanceService.FrCategories.GetValueOrDefault(catId, "Неизвестная");
                    var existing = await _db.FrCategories.FirstOrDefaultAsync(c => c.UserId == userId && c.CategoryId == catId);

                    if (existing != null)
                    {
                        _db.FrCategories.Remove(existing);
                        await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка отключена");
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

                    await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "fr", msgId);
                }

                else if (data == "workspace_menu")
                    await _freelance.ShowWorkspaceMenu(chatId, userId, msgId);

                else if (data.StartsWith("ws_cat_"))
                {
                    var slug = int.Parse( data["ws_cat_".Length..]);
                    var name = FreelanceService.WsCategories.GetValueOrDefault(slug, "Неизвестно");
                    var existing = await _db.WorkspaceCategories
                        .FirstOrDefaultAsync(c => c.UserId == userId && c.CategorySlug == slug);

                    if (existing != null)
                    {
                        _db.WorkspaceCategories.Remove(existing);
                        await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка отключена");
                    }
                    else
                    {
                        _db.WorkspaceCategories.Add(new WorkspaceCategory
                        {
                            UserId = userId,
                            CategorySlug = slug,
                            Name = name,
                            NotificationInterval = "off"
                        });
                        await _db.SaveChangesAsync();
                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка включена! (интервал по умолчанию: off)\nНастрой интервал →");
                    }
                    var catId = int.Parse(data["ws_cat_".Length..]);
                    await _freelance.ShowIntervalSelection(chatId, userId, catId, platform: "ws", msgId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в callback: {ex.Message}");
                await _bot.AnswerCallbackQuery(cb.Id, "Произошла ошибка");
            }
    }
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
