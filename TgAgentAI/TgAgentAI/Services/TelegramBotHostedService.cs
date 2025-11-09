using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgAgentAI.Services
{
    public class TelegramBotHostedService : IHostedService
    {
        private readonly ITelegramBotClient _bot;
        private readonly IContentService _content;
        private readonly string _editorId;
        private readonly string _mediaFolder;

        public TelegramBotHostedService(ITelegramBotClient bot, IContentService content, IConfiguration config)
        {
            _bot = bot;
            _content = content;
            _editorId = config["Telegram:EditorChatId"]!;
            _mediaFolder = config["Bot:MediaFolder"]!;
            Directory.CreateDirectory(_mediaFolder);
        }

        public async Task StartAsync(CancellationToken ct)
        {
            _bot.OnMessage += OnMessage;
            _bot.OnCallbackQuery += OnCallback;
            //await _bot.SetWebhookAsync("https://yourdomain.com/bot"); // или polling
        }

        private async Task OnMessage(ITelegramBotClient bot, Message message, CancellationToken ct)
        {
            if (message.Photo != null || message.Video != null)
            {
                var fileId = message.Photo?.LastOrDefault()?.FileId ?? message.Video!.FileId;
                var file = await bot.GetFile(fileId!, ct);
                var ext = Path.GetExtension(file.FilePath);
                var localPath = Path.Combine(_mediaFolder, $"{file.FileUniqueId}{ext}");
                await using var stream = File.Create(localPath);
                await bot.DownloadFile(file.FilePath!, stream, ct);

                var drafts = await _content.GenerateDraftsAsync(localPath, fileId!, "кейс");

                foreach (var draft in drafts.Take(3))
                {
                    var kb = new InlineKeyboardMarkup(new[]
                    {
                    InlineKeyboardButton.WithCallbackData("ОК", $"approve|{draft.MediaFileId}"),
                    InlineKeyboardButton.WithCallbackData("Правки", $"edit|{draft.MediaFileId}"),
                    InlineKeyboardButton.WithCallbackData("Отложить", $"postpone|{draft.MediaFileId}")
                });

                    await bot.SendPhoto(
                        _editorId,
                        InputFile.FromFileId(draft.MediaFileId),
                        caption: $"<b>{draft.Title}</b>\n\n{draft.Body}\n\n{draft.Hashtags}",
                        parseMode: ParseMode.Html,
                        replyMarkup: kb,
                        cancellationToken: ct);
                }
            }
        }

        private async Task OnCallback(ITelegramBotClient bot, CallbackQuery query, CancellationToken ct)
        {
            var data = query.Data!;
            if (data.StartsWith("approve|"))
            {
                var fileId = data["approve|".Length..];
                // Запланировать публикацию через Hangfire
                await bot.AnswerCallbackQuery(query.Id, "Запланировано!", cancellationToken: ct);
            }
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
