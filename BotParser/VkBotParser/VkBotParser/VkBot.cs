using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VkBotParser.Db;
using VkNet;
using VkNet.Enums.StringEnums;
using VkNet.Model;

namespace VkBotParser
{
    public class VkBot
    {
        private readonly VkApi _api = new();
        private readonly ILogger<VkBot> _log;
        private readonly KworkBotDbContext _db;

        public VkBot(ILogger<VkBot> log, KworkBotDbContext db)
        {
            _log = log;
            _db = db;
        }

        public async Task RunAsync(string accessToken)
        {
            _api.Authorize(new ApiAuthParams { AccessToken = accessToken });

            var longPoll = await _api.Groups.GetLongPollServerAsync(ГРУППА_ID);

            _log.LogInformation("ВК-бот запущен");

            while (true)
            {
                var updates = await _api.Groups.GetBtsAsync(longPoll.Server, longPoll.Key, longPoll.Ts);

                foreach (var update in updates.Updates)
                {
                    if (update.Type == GroupUpdateType.MessageNew)
                    {
                        var msg = update.Message;
                        if (msg.FromId.HasValue)
                        {
                            await HandleMessage(msg.FromId.Value, msg.Text ?? "", msg.Id);
                        }
                    }
                }

                longPoll.Ts = updates.Ts;
                await Task.Delay(500);
            }
        }

        private async Task HandleMessage(long userId, string text, long? messageId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                user = new VkBotParser.Models.User { Id = userId };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
                await SendMessage(userId, "Привет! Это бот мониторинга фриланс-бирж.\nНажми \"Меню\" для настройки.");
            }

            if (text == "/start" || text.ToLower() == "меню")
            {
                await ShowMainMenu(userId);
            }
            // Добавь другие команды (подписки, интервалы и т.д.) — аналогично Telegram
        }

        private async Task ShowMainMenu(long userId)
        {
            var keyboard = new KeyboardBuilder()
                .AddButton("Kwork.ru", "kwork_menu", KeyboardButtonColor.Primary)
                .AddButton("FL.ru", "fl_menu", KeyboardButtonColor.Primary)
                .AddLine()
                .AddButton("YouDo", "youdo_menu", KeyboardButtonColor.Primary)
                .AddButton("Freelance.ru", "fr_menu", KeyboardButtonColor.Primary)
                .AddLine()
                .AddButton("Workspace.ru", "workspace_menu", KeyboardButtonColor.Primary)
                .AddButton("Profi.ru", "profi_menu", KeyboardButtonColor.Primary)
                .AddLine()
                .AddButton("Мои подписки", "my_subscriptions", KeyboardButtonColor.Secondary)
                .AddButton("Помощь", "help", KeyboardButtonColor.Secondary)
                .Build();

            await SendMessage(userId, "<b>Главное меню</b>\nВыбери биржу для настройки:", keyboard);
        }

        private async Task SendMessage(long userId, string text, MessageKeyboard keyboard = null)
        {
            await _api.Messages.SendAsync(new MessagesSendParams
            {
                UserId = userId,
                Message = text,
                Keyboard = keyboard,
                RandomId = Random.Shared.Next()
            });
        }

        // Добавь методы ShowKworkMenu, ShowMySubscriptions и т.д. — копируй из Telegram-бота, только меняй SendMessage на VK-версию
    }
}
