using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ParserFlightTickets.Config;
using ParserFlightTickets.Models;
using ParserFlightTickets.Services.Data;
using Quartz;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ParserFlightTickets.Services.Telegram
{
    public class TelegramPublisher : IHostedService
    {
        private readonly TelegramBotClient _botClient;
        private readonly BotConfig _config;
        private readonly ILogger<TelegramPublisher> _logger;
        private CancellationTokenSource? _cts;
        private readonly IServiceProvider _serviceProvider;
        private readonly SettingsService settings;
        private readonly AppDbContext _db;
        private readonly ISchedulerFactory _schedulerFactory;

        private const string CB_MAIN_AVIA = "main_avia";
        private const string CB_MAIN_OTELI = "main_oteli";
        private const string CB_MAIN_TURY = "main_tury";

        private const string CB_AVIA_MINPRICE = "avia_MinPrice";
        private const string CB_AVIA_MAXPRICE = "avia_maxprice";
        private const string CB_AVIA_INTERVAL = "avia_interval";
        private const string CB_AVIA_MAXPOSTS = "avia_maxposts";
        private const string CB_AVIA_TEMPLATE = "avia_template";
        private const string CB_AVIA_DEPARTCITIES = "avia_departcities";
        private const string CB_AVIA_DESTINATIONS = "avia_destinations";
        private const string CB_AVIA_PRIORITYDEPT = "avia_prioritydept";
        private const string CB_AVIA_PRIORITYDEST = "avia_prioritydest";
        private const string CB_AVIA_BLACKLIST = "avia_blacklist";
        private const string CB_AVIA_BACK = "avia_back";

        private const string CB_AVIA_MAXTRANSFERS = "avia_maxtransfers";
        private const string CB_AVIA_DEPTIME = "avia_deptime";
        private const string CB_AVIA_ADULTS = "avia_adults";
        private const string CB_AVIA_MINDATEDAYS = "avia_mindatedays";
        private const string CB_AVIA_MAXDATEDAYS = "avia_maxdatedays";

        private const string CB_HOTEL_MINPRICE = "hotel_minprice";
        private const string CB_HOTEL_MINSTARS = "hotel_minstars";

        private string _waitingForInput = null;

        public TelegramPublisher(BotConfig config, ILogger<TelegramPublisher> logger, IServiceProvider serviceProvider, SettingsService settingsService, AppDbContext db, ISchedulerFactory schedulerFactory)
        {
            _config = config;
            _logger = logger;
            _botClient = new TelegramBotClient(_config.Telegram.Token);
            _serviceProvider = serviceProvider;
            settings = settingsService;
            _schedulerFactory = schedulerFactory;
            _db = db;
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
            var settings = _serviceProvider.GetRequiredService<SettingsService>();
            if (update.Message is not { } message)
            {
                if (update.CallbackQuery != null)
                {
                    var callback = update.CallbackQuery;
                    var cbChatId = callback.Message.Chat.Id;
                    var cbMessageId = callback.Message.MessageId;
                    var cbData = callback.Data;

                    if (cbChatId != settings.GetLong("Admin1"))
                    {
                        await botClient.AnswerCallbackQuery(callback.Id, "Доступ запрещён");
                        return;
                    }

                    await botClient.AnswerCallbackQuery(callback.Id);

                    switch (cbData)
                    {
                        case CB_AVIA_BACK:
                            await ShowMainMenu(cbChatId, cbMessageId);
                            break;

                        case CB_MAIN_AVIA:
                            await ShowAviaMenu(botClient, cbChatId, settings, cbMessageId);
                            break;

                        // Настройки авиабилетов — ждём ввода
                        case CB_AVIA_MINPRICE:
                            _waitingForInput = "Flights_MinPrice";
                            await botClient.SendMessage(cbChatId, "Введите новое значение MinPrice для авиабилетов:");
                            break;

                        case CB_AVIA_MAXPRICE:
                            _waitingForInput = "Flights_MaxPrice";
                            await botClient.SendMessage(cbChatId, "Введите новое значение MaxPrice для авиабилетов:");
                            break;

                        case CB_AVIA_INTERVAL:
                            _waitingForInput = "Flights_CheckIntervalMinutes";
                            await botClient.SendMessage(cbChatId, "Введите новый интервал проверки (в минутах):");
                            break;

                        case CB_AVIA_MAXPOSTS:
                            _waitingForInput = "Flights_MaxPostsPerDay";
                            await botClient.SendMessage(cbChatId, "Введите максимальное количество постов в день для авиабилетов:");
                            break;

                        case CB_AVIA_MAXTRANSFERS:
                            _waitingForInput = "Flights_MaxTransfers";
                            await botClient.SendMessage(cbChatId, "Макс пересадок (0 = прямые, 1, 2+):");
                            break;

                        case CB_AVIA_DEPTIME:
                            _waitingForInput = "Flights_DepartureTime";
                            await botClient.SendMessage(cbChatId, "Время вылета (any / morning / day / evening):");
                            break;

                        case CB_AVIA_ADULTS:
                            _waitingForInput = "Flights_Adults";
                            await botClient.SendMessage(cbChatId, "Количество человек (1–9):");
                            break;

                        case CB_AVIA_MINDATEDAYS:
                            _waitingForInput = "Flights_MinDateDays";
                            await botClient.SendMessage(cbChatId, "Дата вылета от (дней от сегодня):");
                            break;

                        case CB_AVIA_MAXDATEDAYS:
                            _waitingForInput = "Flights_MaxDateDays";
                            await botClient.SendMessage(cbChatId, "Дата вылета до (дней от сегодня):");
                            break;

                        case CB_AVIA_TEMPLATE:
                            _waitingForInput = "Flights_FlightsTemplate";
                            var currentTemplate = settings.Get("FlightsTemplate", "шаблон не задан");
                            await botClient.SendMessage(cbChatId, $"Текущий шаблон:\n{currentTemplate}\n\nВведите новый шаблон поста (можно использовать ):");
                            break;

                        // Для списков — пока текстовая подсказка (можно потом сделать отдельное меню)
                        case CB_AVIA_DEPARTCITIES:
                        case CB_AVIA_DESTINATIONS:
                        case CB_AVIA_PRIORITYDEPT:
                        case CB_AVIA_PRIORITYDEST:
                        case CB_AVIA_BLACKLIST:
                            string listKey = cbData.Replace("avia_", "");
                            var list = settings.GetList(listKey);
                            string listText = list.Any() ? string.Join(", ", list) : "Список пуст";
                            await botClient.SendMessage(cbChatId,
                                $"{listKey}: {listText}\n\n" +
                                $"Добавить: /add{listKey} MOW\n" +
                                $"Удалить: /remove{listKey} MOW");
                            break;
                    }

                    return;
                }
            }

            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text ?? string.Empty;

            _logger.LogInformation($"Получено сообщение от {chatId}: {text}");

            // 2. Обычное текстовое сообщение (ввод значения настройки)
            if (update.Message?.Text != null && !string.IsNullOrEmpty(_waitingForInput))
            {
                string newValue = update.Message.Text.Trim();
                var part = _waitingForInput.Split('_', StringSplitOptions.RemoveEmptyEntries);
                if (_waitingForInput.StartsWith("Flights_") || _waitingForInput.StartsWith("hotel_") || _waitingForInput.StartsWith("tour_"))
                {
                    settings.Set(_waitingForInput, newValue);
                    await botClient.SendMessage(chatId, $"Значение {part[1]} обновлено на {newValue}");
                }

                _waitingForInput = null;

                // Возвращаем в главное меню или подменю
                await ShowAviaSettings(botClient, chatId); // или другое меню
                return;
            }

            // 3. Обычные текстовые команды (/start, /set, /get и т.д.)
            if (update.Message?.Text?.StartsWith("/") == true)
            {
                if (chatId != settings.GetLong("Admin1"))
                {
                    await botClient.SendMessage(chatId, "Только админ может использовать команды.");
                    return;
                }

                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return;

                string command = parts[0].ToLower();

                switch (command)
                {
                    case "/start":
                        await ShowMainMenu(chatId);
                        break;

                    case "/set":
                        if (parts.Length >= 3)
                        {
                            string key = parts[1];
                            string value = string.Join(" ", parts.Skip(2));
                            settings.Set(key, value);
                            await botClient.SendMessage(chatId, $"Установлено {key} = {value}");

                            // Если изменили интервал — перезапускаем триггер
                            if (key.Equals("CheckIntervalMinutes", StringComparison.OrdinalIgnoreCase) &&
                                int.TryParse(value, out int newInterval) && newInterval > 0)
                            {
                                try
                                {
                                    var scheduler = await _schedulerFactory.GetScheduler();
                                    var triggerKey = new TriggerKey("FlightTrigger");

                                    if (await scheduler.CheckExists(triggerKey))
                                    {
                                        var newTrigger = TriggerBuilder.Create()
                                            .WithIdentity(triggerKey)
                                            .ForJob("FlightSearchJob")
                                            .StartNow()
                                            .WithSimpleSchedule(x => x
                                                .WithIntervalInMinutes(newInterval)
                                                .RepeatForever())
                                            .Build();

                                        await scheduler.RescheduleJob(triggerKey, newTrigger);
                                        await botClient.SendMessage(chatId, $"Интервал проверки обновлён до {newInterval} мин");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Ошибка обновления триггера");
                                    await botClient.SendMessage(chatId, "Ошибка при обновлении интервала");
                                }
                            }
                        }
                        else
                        {
                            await botClient.SendMessage(chatId, "Использование: /set MinPrice 7000");
                        }
                        break;

                    case "/get":
                        if (parts.Length >= 2)
                        {
                            string key = parts[1];
                            string val = settings.Get(key, "не найдено");
                            await botClient.SendMessage(chatId, $"{key}: {val}");
                        }
                        break;

                    case "/adddepart":
                    case "/adddest":
                    case "/addprioritydept":
                    case "/addprioritydest":
                    case "/addblacklist":
                        string listKey = command.Replace("/add", "");
                        if (parts.Length >= 2)
                        {
                            string city = parts[1].ToUpper();
                            var list = settings.GetList(listKey);
                            if (!list.Contains(city))
                            {
                                list.Add(city);
                                settings.SetList(listKey, list);
                                await botClient.SendMessage(chatId, $"Добавлено: {city} в {listKey}");
                            }
                            else
                            {
                                await botClient.SendMessage(chatId, "Город уже есть в списке");
                            }
                        }
                        break;

                    case "/removedepart":
                    case "/removedest":
                    case "/removeprioritydept":
                    case "/removeprioritydest":
                    case "/removeblacklist":
                        string removeKey = command.Replace("/remove", "");
                        if (parts.Length >= 2)
                        {
                            string city = parts[1].ToUpper();
                            var list = settings.GetList(removeKey);
                            if (list.Remove(city))
                            {
                                settings.SetList(removeKey, list);
                                await botClient.SendMessage(chatId, $"Удалено: {city} из {removeKey}");
                            }
                            else
                            {
                                await botClient.SendMessage(chatId, "Город не найден в списке");
                            }
                        }
                        break;

                    default:
                        await botClient.SendMessage(chatId, "Неизвестная команда. Используй кнопки или /start");
                        break;
                }
            }
        }

        private async Task ShowMainMenu(long chatId, int? messageId = null)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
    {
        InlineKeyboardButton.WithCallbackData("✈️ Авиабилеты", CB_MAIN_AVIA),
        InlineKeyboardButton.WithCallbackData("🏨 Отели (скоро)", CB_MAIN_OTELI),
        InlineKeyboardButton.WithCallbackData("🌴 Туры (скоро)", CB_MAIN_TURY)
    });

            string text = "Админ-панель\nВыберите раздел настроек:";

            if (messageId.HasValue)
            {
                await _botClient.EditMessageText(chatId, messageId.Value, text, replyMarkup: keyboard);
            }
            else
            {
                await _botClient.SendMessage(chatId, text, replyMarkup: keyboard);
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            _logger.LogError(exception, "Ошибка в Telegram-боте");
            return Task.CompletedTask;
        }

        // Метод для публикации в канал (будем использовать из других мест)
        public async Task PublishToChannelAsync(string text, string? imageBase64 = null)
        {
            try
            {
                if (string.IsNullOrEmpty(imageBase64))
                {
                    await _botClient.SendMessage(
                        chatId: _config.Telegram.ChannelId,
                        text: text,
                        parseMode: ParseMode.Html);
                }
                else
                {
                    byte[] bytes = Convert.FromBase64String(imageBase64);
                    await _botClient.SendPhoto(
                        chatId: _config.Telegram.ChannelId,
                        photo: InputFile.FromStream(new MemoryStream(bytes), "photo.jpg"),
                        caption: text,
                        parseMode: ParseMode.Html);
                }

                _logger.LogInformation("Пост опубликован");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка публикации");
            }
        }

        private async Task  ShowAviaMenu(ITelegramBotClient botClient, long chatId, SettingsService settings, int? messageId = null)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
        new[] { InlineKeyboardButton.WithCallbackData($"Min цена: {settings.GetInt("Flights_MinPrice")} ₽", CB_AVIA_MINPRICE) },
        new[] { InlineKeyboardButton.WithCallbackData($"Max цена: {settings.GetInt("Flights_MaxPrice")} ₽", CB_AVIA_MAXPRICE) },
        new[] { InlineKeyboardButton.WithCallbackData($"Интервал проверки: {settings.GetInt("Flights_CheckIntervalMinutes")} мин", CB_AVIA_INTERVAL) },
        new[] { InlineKeyboardButton.WithCallbackData($"Макс постов/день: {settings.GetInt("Flights_MaxPostsPerDay")}", CB_AVIA_MAXPOSTS) },
        new[] { InlineKeyboardButton.WithCallbackData($"Макс пересадок: {settings.GetInt("Flights_MaxTransfers")}", CB_AVIA_MAXTRANSFERS) },
        new[] { InlineKeyboardButton.WithCallbackData($"Время вылета: {settings.Get("Flights_DepartureTime")}", CB_AVIA_DEPTIME) },
        new[] { InlineKeyboardButton.WithCallbackData($"Кол-во человек: {settings.GetInt("Flights_Adults")}", CB_AVIA_ADULTS) },
        new[] { InlineKeyboardButton.WithCallbackData($"Дата от: +{settings.GetInt("Flights_MinDateDays")} дн", CB_AVIA_MINDATEDAYS) },
        new[] { InlineKeyboardButton.WithCallbackData($"Дата до: +{settings.GetInt("Flights_MaxDateDays")} дн", CB_AVIA_MAXDATEDAYS) },
        new[] { InlineKeyboardButton.WithCallbackData("Шаблон поста ✏️", CB_AVIA_TEMPLATE) },
        new[] { InlineKeyboardButton.WithCallbackData("Города вылета", CB_AVIA_DEPARTCITIES) },
        new[] { InlineKeyboardButton.WithCallbackData("Города прилёта", CB_AVIA_DESTINATIONS) },
        new[] { InlineKeyboardButton.WithCallbackData("Приоритет вылета", CB_AVIA_PRIORITYDEPT) },
        new[] { InlineKeyboardButton.WithCallbackData("Приоритет прилёта", CB_AVIA_PRIORITYDEST) },
        new[] { InlineKeyboardButton.WithCallbackData("Чёрный список", CB_AVIA_BLACKLIST) },
        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад в главное меню", CB_AVIA_BACK) }
    });
            if (messageId.HasValue)
            {
                await _botClient.EditMessageText(
                chatId,
                messageId.Value,
                "Настройки авиабилетов:",
                replyMarkup: keyboard
            );
            }
            else
            {
                await _botClient.SendMessage(chatId, "Настройки авиабилетов:",
                replyMarkup: keyboard);
            }
        }

        private async Task SendToAdminAsync(string text)
        {
            try
            {
                await _botClient.SendMessage(settings.GetLong("Admin1"), text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось отправить сообщение админу");
            }
        }

        private async Task ShowAviaSettings(ITelegramBotClient botClient, long chatId, int? messageId = null)
        {
            var settings = _serviceProvider.GetRequiredService<SettingsService>();

            var keyboard = new InlineKeyboardMarkup(new[]
            {
        new[] { InlineKeyboardButton.WithCallbackData($"Min цена: {settings.GetInt("Flights_MinPrice")} ₽", CB_AVIA_MINPRICE) },
        new[] { InlineKeyboardButton.WithCallbackData($"Max цена: {settings.GetInt("Flights_MaxPrice")} ₽", CB_AVIA_MAXPRICE) },
        new[] { InlineKeyboardButton.WithCallbackData($"Интервал проверки: {settings.GetInt("Flights_CheckIntervalMinutes")} мин", CB_AVIA_INTERVAL) },
        new[] { InlineKeyboardButton.WithCallbackData($"Макс постов/день: {settings.GetInt("Flights_MaxPostsPerDay")}", CB_AVIA_MAXPOSTS) },
        new[] { InlineKeyboardButton.WithCallbackData($"Макс пересадок: {settings.GetInt("Flights_MaxTransfers")}", CB_AVIA_MAXTRANSFERS) },
        new[] { InlineKeyboardButton.WithCallbackData($"Время вылета: {settings.Get("Flights_DepartureTime")}", CB_AVIA_DEPTIME) },
        new[] { InlineKeyboardButton.WithCallbackData($"Кол-во человек: {settings.GetInt("Flights_Adults")}", CB_AVIA_ADULTS) },
        new[] { InlineKeyboardButton.WithCallbackData($"Дата от: +{settings.GetInt("Flights_MinDateDays")} дн", CB_AVIA_MINDATEDAYS) },
        new[] { InlineKeyboardButton.WithCallbackData($"Дата до: +{settings.GetInt("Flights_MaxDateDays")} дн", CB_AVIA_MAXDATEDAYS) },
        new[] { InlineKeyboardButton.WithCallbackData("Шаблон поста ✏️", CB_AVIA_TEMPLATE) },
        new[] { InlineKeyboardButton.WithCallbackData("Города вылета", CB_AVIA_DEPARTCITIES) },
        new[] { InlineKeyboardButton.WithCallbackData("Города прилёта", CB_AVIA_DESTINATIONS) },
        new[] { InlineKeyboardButton.WithCallbackData("Приоритет вылета", CB_AVIA_PRIORITYDEPT) },
        new[] { InlineKeyboardButton.WithCallbackData("Приоритет прилёта", CB_AVIA_PRIORITYDEST) },
        new[] { InlineKeyboardButton.WithCallbackData("Чёрный список", CB_AVIA_BLACKLIST) },
        new[] { InlineKeyboardButton.WithCallbackData("◀️ Назад в главное меню", CB_AVIA_BACK) }
    });

            if (messageId.HasValue)
            {
                await _botClient.EditMessageText(
                chatId,
                messageId.Value,
                "Настройки авиабилетов:",
                replyMarkup: keyboard
            );
            }
            else
            {
                await _botClient.SendMessage(chatId, "Настройки авиабилетов:",
                replyMarkup: keyboard);
            }
        }
    }
}
