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
            await _freelance.EnsureUserExists(userId, username);
            var msgId = cb.Message.MessageId;
        var mId = cb.Id;

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
                            NotificationInterval = "instant" // сразу включаем на максимум
                        });
                        await _db.SaveChangesAsync();

                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка включена! Настрой интервал →");

                        // ←←←← СРАЗУ ПРОВАЛИВАЕМСЯ В ВЫБОР ИНТЕРВАЛА
                        await _freelance.ShowIntervalSelection(chatId, userId, catId, isKwork: true, msgId);
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
                            NotificationInterval = "instant"
                        });
                        await _db.SaveChangesAsync();

                        await _bot.AnswerCallbackQuery(cb.Id, "Подписка включена! Настрой интервал →");
                        await _freelance.ShowIntervalSelection(chatId, userId, catId, isKwork: false, msgId);
                    }
                }

                else if (data.StartsWith("edit_interval_kwork_"))
                {
                    var catId = int.Parse(data["edit_interval_kwork_".Length..]);
                    await _freelance.ShowIntervalSelection(chatId, userId, catId, isKwork: true, msgId);
                }

                else if (data.StartsWith("edit_interval_fl_"))
                {
                    var catId = int.Parse(data["edit_interval_fl_".Length..]);
                    await _freelance.ShowIntervalSelection(chatId, userId, catId, isKwork: false, msgId);
                }

                else if (data.StartsWith("kwork_setint_") || data.StartsWith("fl_setint_"))
                {
                    var parts = data.Split('_');
                    var isKwork = parts[0] == "kwork";
                    var catId = int.Parse(parts[2]);
                    var interval = parts[3];

                    await _freelance.SetNotificationInterval(userId, catId, interval, isKwork);

                    await _bot.AnswerCallbackQuery(cb.Id, $"Интервал: {GetPrettyInterval(interval)}");

                    // Возвращаемся в "Мои подписки"
                    await _freelance.ShowMySubscriptions(chatId, userId, msgId);
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
