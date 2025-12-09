using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BotParser.Services
{
    public class MobileProxyService
    {
        private readonly HttpClient _client;
        private readonly string _changeIpUrl;   // например: https://changeip.mobileproxy.space/?proxy_key=abc123
        private readonly string _checkIpUrl;    // например: https://mobileproxy.space/api.html?command=proxy_ip&proxy_id=12345

        public MobileProxyService(string changeIpUrl, string checkIpUrl, string bearerToken)
        {
            _changeIpUrl = changeIpUrl;
            _checkIpUrl = checkIpUrl;

            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");
            _client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0 Safari/537.36");
        }

        public async Task<string?> RotateAndVerifyIpAsync(ILogger logger)
        {
            string? oldIp = await GetCurrentIpAsync(logger);
            logger.LogInformation("Текущий IP: {OldIp}", oldIp ?? "неизвестно");

            // Меняем IP
            try
            {
                var response = await _client.GetAsync(_changeIpUrl);
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(json);

                if (result.GetProperty("status").GetString() == "OK")
                {
                    logger.LogInformation("Запрос на смену IP отправлен успешно");
                    Console.WriteLine("Запрос на смену IP отправлен успешно");
                }
                else
                {
                    var msg = result.TryGetProperty("message", out var m) ? m.GetString() : "неизвестно";
                    logger.LogWarning("Не удалось сменить IP: {Message}", msg);
                    Console.WriteLine("Не удалось сменить IP");
                    return null;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при запросе смены IP");
                Console.WriteLine("Ошибка при запросе смены IP");
                return null;
            }

            // Ждём 10–25 сек (зависит от оператора)
            await Task.Delay(TimeSpan.FromSeconds(15 + Random.Shared.Next(0, 10)));

            // Проверяем новый IP
            string? newIp = await GetCurrentIpAsync(logger);

            if (newIp != null && newIp != oldIp)
            {
                logger.LogInformation("IP УСПЕШНО СМЕНЁН → {NewIp} (было: {OldIp})", newIp, oldIp ?? "неизвестно");
                Console.WriteLine("IP УСПЕШНО СМЕНЁН");
                return newIp;
            }
            else
            {
                logger.LogWarning("IP НЕ СМЕНЯЛСЯ! Текущий: {NewIp} (ожидался новый)", newIp ?? "null");
                Console.WriteLine("IP НЕ СМЕНЯЛСЯ!");
                return null;
            }
        }

        private async Task<string?> GetCurrentIpAsync(ILogger logger)
        {
            try
            {
                var response = await _client.GetAsync(_checkIpUrl);
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(json);

                if (result.GetProperty("status").GetString() == "OK")
                {
                    var proxyData = result.GetProperty("ip");
                    return proxyData.GetString();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при проверке текущего IP");
                Console.WriteLine("Ошибка при проверке текущего IP");
            }
            return null;
        }
    }
}
