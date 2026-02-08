using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParserFlightTickets.Config;
using ParserFlightTickets.Services.Api;
using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ParserFlightTickets.Services.Telegram
{
    public class TelegramPublisher : IHostedService
    {
        private readonly TelegramBotClient _botClient;
        private readonly BotConfig _config;
        private readonly ILogger<TelegramPublisher> _logger;
        private CancellationTokenSource? _cts;
        private readonly IServiceProvider _serviceProvider;

        public TelegramPublisher(BotConfig config, ILogger<TelegramPublisher> logger, IServiceProvider serviceProvider)
        {
            _config = config;
            _logger = logger;
            _botClient = new TelegramBotClient(_config.Telegram.Token);
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // получаем все типы
            };

            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                _cts.Token);

            _logger.LogInformation("Telegram бот запущен и слушает обновления");

            // Тестовое сообщение админу при старте
            SendToAdminAsync("Бот запущен и готов к работе!").GetAwaiter().GetResult();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts?.Cancel();
            _logger.LogInformation("Telegram бот остановлен");
            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.Message is not { } message)
                return;

            var chatId = message.Chat.Id;
            var text = message.Text ?? string.Empty;

            _logger.LogInformation($"Получено сообщение от {chatId}: {text}");

            // Только админ может управлять
            if (chatId != _config.Telegram.AdminUserId)
            {
                if (text.StartsWith("/start"))
                {
                    await botClient.SendMessage(chatId, "Привет! Это бот для поиска горящих предложений. Только админ может управлять мной.");
                }
                return;
            }

            switch (text.ToLower())
            {
                case "/start":
                    await botClient.SendMessage(chatId,
                        "Привет, админ! Бот работает.\n\nКоманды:\n/status - статус\n/testpublish - тестовый пост в канал");
                    break;

                case "/status":
                    await botClient.SendMessage(chatId, $"Статус: активен\nКанал: {_config.Telegram.ChannelId}\nИнтервал проверки: {_config.SearchSettings.CheckIntervalMinutes} мин");
                    break;

                case "/testpublish":
                    await PublishToChannelAsync(
                        "🧪 *Тестовый пост*\n\nЭто проверка публикации в канал.\nЦена: 9999 ₽\n[Ссылка](https://www.aviasales.ru)",
                        "https://picsum.photos/800/600"); // тестовая картинка
                    await botClient.SendMessage(chatId, "Тестовый пост отправлен в канал!");
                    break;
                case "/testflight":
                    var service = _serviceProvider.GetRequiredService<TravelPayoutsService>(); // !!! нужно внедрить IServiceProvider
                    var deals = await service.SearchOneWayByPriceRangeAsync("MOW", "UFA"); // пример на март 2025

                    if (deals?.Any() == true)
                    {
                        var best = deals.OrderBy(d => d.Price).First();
                        string texts = $"✈️ Найден дешёвый билет!\n" +
                                      $"Из {best.Origin} в {best.Destination}\n" +
                                      $"Дата: {best.DepartureDate}\n" +
                                      $"Цена: {best.Price} ₽\n" +
                                      $"Авиакомпания: {best.Airline}\n" +
                                      $"[Купить на Aviasales]({best.AffiliateLink})";

                        await PublishToChannelAsync(texts, "https://picsum.photos/800/600?random=1");
                        await botClient.SendMessage(chatId, "Тестовый поиск выполнен и пост отправлен!");
                    }
                    else
                    {
                        await botClient.SendMessage(chatId, "Ничего не найдено или ошибка запроса.");
                    }
                    break;
                //case "/finddeals":
                //    var apiService = _serviceProvider.GetRequiredService<TravelPayoutsService>();

                //    var origins = _config.SearchSettings.RussianCities.Take(5);    // можно увеличить
                //    var destinations = _config.SearchSettings.PopularDestinations.Take(5);

                //    int publishedCount = 0;

                //    foreach (var origin in origins)
                //    {
                //        foreach (var dest in destinations)
                //        {
                //            var deal = await apiService.SearchCheapFlightsAsync(origin, dest);

                //            if (deal == null || !deal.Any())
                //                continue;

                //            // Берём только 1–2 самых дешёвых на маршрут, чтобы не заспамить
                //            var topDeals = deal.OrderBy(d => d.Price).Take(2);

                //            foreach (var dealss in topDeals)
                //            {
                //                // Короткий шаблон (чтобы влез в 1024 символа)
                //                string postText = $"✈️ **{origin} → {dealss.Destination}**\n" +
                //                                  $"Дата: {dealss.DepartureDate?.Split('T')?[0] ?? "ближайшая"}\n" +
                //                                  $"Цена: **{dealss.Price} ₽**\n" +
                //                                  $"Авиакомпания: {dealss.Airline ?? "—"}\n" +
                //                                  $"[Купить на Aviasales]({dealss.AffiliateLink})";

                //                string imageUrl = GetPlaceholderImage();

                //                await PublishToChannelAsync(postText, imageUrl);

                //                publishedCount++;

                //                // Пауза между постами — важно!
                //                await Task.Delay(1500);  // 1.5 секунды
                //            }

                //            await Task.Delay(3000);  // чуть больше паузы между разными маршрутами
                //        }
                //    }

                //    await botClient.SendMessage(chatId,
                //        $"Поиск завершён.\nОпубликовано **{publishedCount}** предложений в канал.\n" +
                //        $"Проверь канал, если ничего нет — смотри логи (возможно, цены не прошли фильтр или API ничего не вернул).");

                //    break;

                default:
                    await botClient.SendMessage(chatId, "Неизвестная команда. Попробуй /status или /testpublish");
                    break;
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            _logger.LogError(exception, "Ошибка в Telegram-боте");
            return Task.CompletedTask;
        }

        private string GetPlaceholderImage()
        {
            // Случайная картинка каждый раз (picsum.photos — надёжный заглушка)
            int random = new Random().Next(100, 1000);
            return $"https://picsum.photos/seed/travel{random}/800/600";
        }

        // Метод для публикации в канал (будем использовать из других мест)
        public async Task PublishToChannelAsync(string text, string? imageUrl = null)
        {
            try
            {
                // Экранируем текст
                string safeText = EscapeMarkdownV2(text);

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    await _botClient.SendPhoto(
                        chatId: _config.Telegram.ChannelId,
                        photo: InputFile.FromUri(imageUrl),
                        caption: safeText,
                        parseMode: ParseMode.MarkdownV2,
                        cancellationToken: CancellationToken.None);
                }
                else
                {
                    await _botClient.SendMessage(
                        chatId: _config.Telegram.ChannelId,
                        text: safeText,
                        parseMode: ParseMode.MarkdownV2,
                        cancellationToken: CancellationToken.None);
                }

                _logger.LogInformation("Пост опубликован в канал");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка публикации в канал. Исходный текст: {Text}", text);
            }
        }

        private async Task SendToAdminAsync(string text)
        {
            try
            {
                await _botClient.SendMessage(_config.Telegram.AdminUserId, text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось отправить сообщение админу");
            }
        }

        private static string EscapeMarkdownV2(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Символы, которые нужно экранировать везде (кроме внутри некоторых сущностей, но мы делаем полное экранирование)
            var specialChars = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };

            var sb = new StringBuilder(text.Length * 2); // грубо, на запас

            foreach (char c in text)
            {
                if (specialChars.Contains(c))
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
