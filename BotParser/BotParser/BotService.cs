using BotParser.Db;
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
        private readonly KworkService _kwork;
        private readonly KworkBotDbContext _db;

        private readonly InlineKeyboardMarkup _mainMenu = new(new[]
{
    new[] { InlineKeyboardButton.WithCallbackData("Выбрать биржу", "select_exchange") },
    new[] { InlineKeyboardButton.WithCallbackData("Мои подписки", "my_subscriptions") },
    new[] { InlineKeyboardButton.WithCallbackData("Настроить уведомления", "set_interval") }
});

        public BotService(ITelegramBotClient bot, IServiceProvider sp, KworkService kwork, KworkBotDbContext db)
        {
            _bot = bot;
            _sp = sp;
            _kwork = kwork;
            _db = db;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _bot.StartReceiving(HandleUpdate, HandleError, cancellationToken: stoppingToken);
            Console.WriteLine("Бот запущен!");
            await Task.Delay(-1, stoppingToken);
        }

        private async Task HandleUpdate(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            if (update.Message is { } message)
            {
                if (message.Text == "/start")
                {
                    await _kwork.ShowMainMenu(message.Chat.Id);
                    return;
                }
            }

            else if (update.CallbackQuery is { } cb)
            {
                var data = cb.Data!;
                var chatId = cb.Message!.Chat.Id;
                var userId = cb.From.Id;
                var msgId = cb.Message.MessageId;

                if (data == "main_menu")
                    await _kwork.ShowMainMenu(chatId, msgId);
                else if (data == "show_categories")
                    await _kwork.ShowCategories(chatId, userId, msgId);
                else if (data.StartsWith("select_cat_"))
                {
                    var catId = int.Parse(data["select_cat_".Length..]);
                    await _kwork.TryDeleteMessage(chatId, msgId);
                    await _kwork.SelectCategory(userId, catId, chatId, msgId);
                }
                else if (data.StartsWith("set_interval_"))
                {
                    var parts = data.Split('_'); // set_interval_11_instant
                    var catId = int.Parse(parts[2]);
                    var interval = parts[3];
                    await _kwork.SetIntervalForCategory(userId, catId, interval, chatId, msgId);
                }

                await _bot.AnswerCallbackQuery(cb.Id);
            }
        }

        private Task HandleError(ITelegramBotClient bot, Exception ex, CancellationToken ct)
        {
            Console.WriteLine(ex);
            return Task.CompletedTask;
        }
    }
}
