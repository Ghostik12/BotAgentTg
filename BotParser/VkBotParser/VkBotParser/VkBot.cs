using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Web;
using VkBotParser.Db;
using VkBotParser.Models;
using VkNet;
using VkNet.Enums;
using VkNet.Enums.StringEnums;
using VkNet.Model;
using VkNet.Utils.JsonConverter;

namespace VkBotParser
{
    public class VkBot
    {
        private readonly ILogger<VkBot> _log;
        private readonly KworkBotDbContext _db;
        private readonly HttpClient _httpClient = new HttpClient();

        private const string ApiVersion = "5.199";
        private const long GroupId = 235726596L; // твой ID группы (положительный!)
        private readonly IOptions<VkSettings> _vkSettings;

        public VkBot( ILogger<VkBot> log, KworkBotDbContext db, IOptions<VkSettings> vkSettings)
        {
            _log = log;
            _db = db;
            _vkSettings = vkSettings ?? throw new ArgumentNullException(nameof(vkSettings));
        }

        public async Task RunAsync()
        {
            _log.LogInformation("VK бот запущен (чистый HTTP + Long Poll)");
            string accessToken = _vkSettings.Value.AccessToken;
            string? server = null;
            string? key = null;
            string? ts = null;

            while (true)
            {
                try
                {
                    // Получаем Long Poll сервер, если данных нет
                    if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(ts))
                    {
                        var lpParams = new Dictionary<string, string>
                        {
                            ["group_id"] = GroupId.ToString(),
                            ["access_token"] = accessToken,
                            ["v"] = ApiVersion
                        };

                        var lpResponse = await PostApi("groups.getLongPollServer", lpParams);

                        if (lpResponse?.TryGetProperty("response", out JsonElement resp) == true)
                        {
                            server = resp.GetProperty("server").GetString();
                            key = resp.GetProperty("key").GetString();
                            ts = resp.GetProperty("ts").GetString();

                            _log.LogInformation("LongPoll сервер получен: server={Server}, key={Key}, ts={Ts}", server, key, ts);
                        }
                        else
                        {
                            _log.LogWarning("Не удалось получить LongPoll сервер");
                            await Task.Delay(5000);
                            continue;
                        }
                    }

                    // Запрос к Long Poll
                    string lpUrl = $"{server}?act=a_check&key={HttpUtility.UrlEncode(key!)}&ts={ts}&wait=25&mode=2";

                    var lpJsonStr = await _httpClient.GetStringAsync(lpUrl);
                    var lpDoc = JsonDocument.Parse(lpJsonStr);

                    if (lpDoc.RootElement.TryGetProperty("failed", out var failedElem))
                    {
                        int failCode = failedElem.GetInt32();
                        _log.LogWarning("Long Poll failed код: {Code}", failCode);

                        if (failCode == 1 && lpDoc.RootElement.TryGetProperty("ts", out var newTsElem))
                        {
                            ts = newTsElem.GetString();
                        }
                        else if (failCode == 2 || failCode == 3)
                        {
                            server = key = ts = null; // переподключаемся
                        }
                        continue;
                    }

                    // Обновляем ts
                    if (lpDoc.RootElement.TryGetProperty("ts", out var tsElem))
                        ts = tsElem.GetString();

                    // Обработка обновлений
                    if (lpDoc.RootElement.TryGetProperty("updates", out var updates) && updates.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var update in updates.EnumerateArray())
                        {
                            string type = update.GetProperty("type").GetString() ?? "";

                            if (type == "message_new")
                            {
                                var obj = update.GetProperty("object");
                                var msg = obj.GetProperty("message");

                                long fromId = msg.GetProperty("from_id").GetInt64();
                                long peerId = msg.GetProperty("peer_id").GetInt64();
                                string text = msg.TryGetProperty("text", out var txt) ? txt.GetString() ?? "" : "";

                                if (fromId > 0 && !string.IsNullOrWhiteSpace(text))
                                {
                                    await HandleMessage(peerId, fromId, text);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Ошибка в LongPoll цикле");
                    await Task.Delay(5000);
                    server = key = ts = null;
                }
            }
        }

        private async Task HandleMessage(long peerId, long userId, string text)
        {
            var lowerText = text.Trim().ToLowerInvariant();

            //var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            //if (user == null)
            //{
                //user = new VkBotParser.Models.User { Id = userId };
                //_db.Users.Add(user);
                //await _db.SaveChangesAsync();

                await SendMessage(peerId, "Привет! Это бот мониторинга фриланс-бирж.\nНажми «Меню» для настройки.");
            //}

            if (lowerText == "/start" || lowerText == "меню" || lowerText.Contains("меню"))
            {
                await ShowMainMenu(peerId);
                return;
            }

            // Пока простая заглушка
            await SendMessage(peerId, $"Получено: {text}\n\nПока умею только меню по /start или «меню»");
        }

        private async Task ShowMainMenu(long peerId)
        {
            var keyboardJson = @"{
    ""one_time"": false,
    ""buttons"": [
        [
            {""action"": {""type"": ""text"", ""label"": ""Kwork.ru"", ""payload"": ""{\""button\"":\""kwork_menu\""}""}, ""color"": ""primary""},
            {""action"": {""type"": ""text"", ""label"": ""FL.ru"", ""payload"": ""{\""button\"":\""fl_menu\""}""}, ""color"": ""primary""}
        ],
        [
            {""action"": {""type"": ""text"", ""label"": ""YouDo"", ""payload"": ""{\""button\"":\""youdo_menu\""}""}, ""color"": ""primary""},
            {""action"": {""type"": ""text"", ""label"": ""Freelance.ru"", ""payload"": ""{\""button\"":\""fr_menu\""}""}, ""color"": ""primary""}
        ],
        [
            {""action"": {""type"": ""text"", ""label"": ""Workspace.ru"", ""payload"": ""{\""button\"":\""workspace_menu\""}""}, ""color"": ""primary""},
            {""action"": {""type"": ""text"", ""label"": ""Profi.ru"", ""payload"": ""{\""button\"":\""profi_menu\""}""}, ""color"": ""primary""}
        ],
        [
            {""action"": {""type"": ""text"", ""label"": ""Мои подписки"", ""payload"": ""{\""button\"":\""my_subscriptions\""}""}, ""color"": ""secondary""},
            {""action"": {""type"": ""text"", ""label"": ""Помощь"", ""payload"": ""{\""button\"":\""help\""}""}, ""color"": ""secondary""}
        ]
    ]
}";

            await SendMessage(peerId,
                "<b>Главное меню</b>\nВыбери биржу для настройки:",
                keyboardJson);
        }

        private async Task SendMessage(long peerId, string message, string? keyboardJson = null)
        {
            var paramsDict = new Dictionary<string, string>
            {
                ["peer_id"] = peerId.ToString(),
                ["message"] = message,
                ["random_id"] = Random.Shared.Next().ToString(),
                ["dont_parse_links"] = "1"
            };

            if (!string.IsNullOrEmpty(keyboardJson))
            {
                paramsDict["keyboard"] = keyboardJson;
            }

            var response = await PostApi("messages.send", paramsDict);

            if (response == null)
            {
                _log.LogWarning("Не удалось отправить сообщение peerId={PeerId}", peerId);
            }
            else
            {
                _log.LogInformation("Сообщение отправлено peerId={PeerId}", peerId);
            }
        }

        private async Task<JsonElement?> PostApi(string method, Dictionary<string, string> parameters)
        {
            parameters["access_token"] = _vkSettings.Value.AccessToken; // если не передан — ошибка
            parameters["v"] = ApiVersion;

            var content = new FormUrlEncodedContent(parameters);

            try
            {
                var resp = await _httpClient.PostAsync($"https://api.vk.com/method/{method}", content);
                var jsonStr = await resp.Content.ReadAsStringAsync();

                _log.LogDebug("Ответ от {Method}: {Json}", method, jsonStr);

                if (jsonStr.Contains("\"error\""))
                {
                    _log.LogError("API ошибка в {Method}: {Json}", method, jsonStr);
                    return null;
                }

                var doc = JsonDocument.Parse(jsonStr);
                return doc.RootElement;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Ошибка запроса к {Method}", method);
                return null;
            }
        }
    }

    // Добавь методы ShowKworkMenu, ShowMySubscriptions и т.д. — копируй из Telegram-бота, только меняй SendMessage на VK-версию
}
