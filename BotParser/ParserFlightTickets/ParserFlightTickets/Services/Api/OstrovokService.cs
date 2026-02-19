using Microsoft.Extensions.Logging;
using ParserFlightTickets.Models;
using ParserFlightTickets.Services.Data;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ParserFlightTickets.Services.Api
{
    public class OstrovokService
    {
        private readonly HttpClient _httpClient;
        private readonly SettingsService _settings;
        private readonly ILogger<OstrovokService> _logger;

        public OstrovokService(SettingsService settings, ILogger<OstrovokService> logger)
        {
            _settings = settings;
            _logger = logger;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            // Ostrovok требует авторизацию через API-ключ в заголовке или query
            // Если у тебя есть ключ — добавь его в настройки БД и сюда
            // string apiKey = _settings.Get("OstrovokApiKey");
            // _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        public async Task<List<HotelDeal>?> SearchHotelsAsync(string cityCode, DateTime checkIn, DateTime checkOut)
        {
            try
            {
                // Пример запроса — адаптируй под реальный API Ostrovok
                // https://partner.ostrovok.ru/api/v1/hotels/search?city_id=123&checkin=2025-03-01&checkout=2025-03-05&adults=2&currency=RUB
                string url = $"https://partner.ostrovok.ru/api/v1/hotels/search" +
                             $"?city_id={cityCode}" +  // или city_name, зависит от API
                             $"&checkin={checkIn:yyyy-MM-dd}" +
                             $"&checkout={checkOut:yyyy-MM-dd}" +
                             $"&adults=2" +  // пока фиксируем 2, потом из настроек
                             $"&currency=RUB" +
                             $"&limit=10" +
                             $"&sort=price_asc" +
                             $"&min_stars={_settings.GetInt("MinStarsHotels", 3)}" +
                             $"&min_rating={_settings.Get("MinRatingHotels", "7.5")}" +
                             $"&meal={_settings.Get("MealTypeHotels", "")}";  // all_inclusive, breakfast и т.д.

                _logger.LogInformation("Ostrovok запрос: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ostrovok ошибка: {Status}", response.StatusCode);
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                // Парсинг зависит от реальной структуры ответа Ostrovok
                // Пример — адаптируй под их документацию
                if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                    return null;

                var deals = new List<HotelDeal>();

                foreach (var item in data.EnumerateArray())
                {
                    if (!item.TryGetProperty("price", out var p) || !p.TryGetDecimal(out decimal price))
                        continue;

                    var deal = new HotelDeal
                    {
                        CityCode = cityCode,
                        HotelName = item.TryGetProperty("name", out var n) ? n.GetString() : "Отель",
                        Stars = item.TryGetProperty("stars", out var st) && st.TryGetInt32(out int stars) ? stars : 0,
                        Rating = item.TryGetProperty("rating", out var r) && r.TryGetDecimal(out decimal rat) ? rat : 0,
                        PricePerNight = (int)price,
                        MealType = item.TryGetProperty("meal", out var m) ? m.GetString() : "",
                        CheckIn = checkIn,
                        CheckOut = checkOut,
                        // Ссылка — адаптируй
                        AffiliateLink = $""
                    };

                    deals.Add(deal);
                }

                return deals.Any() ? deals : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка Ostrovok поиска");
                return null;
            }
        }
    }
}
