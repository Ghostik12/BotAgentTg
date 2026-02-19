using Microsoft.Extensions.Logging;
using ParserFlightTickets.Config;
using ParserFlightTickets.Models;
using ParserFlightTickets.Services.Data;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ParserFlightTickets.Services.Api
{
    public class LevelTravelService
    {
        private readonly HttpClient _httpClient;
        private readonly SettingsService _settings;
        private readonly ILogger<LevelTravelService> _logger;
        private readonly BotConfig _config;

        public LevelTravelService(SettingsService settings, ILogger<LevelTravelService> logger, BotConfig config)
        {
            _settings = settings;
            _logger = logger;

            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.leveltravel.v3.7"));
            _config = config;
        }

        /// <summary>
        /// 1. Ставим поиск в очередь → возвращаем request_id
        /// </summary>
        public async Task<string?> EnqueueSearchAsync(string fromCity, string toCountry, DateTime startDate, int nightsMin, int nightsMax, int adults = 2)
        {
            var apiKey = _config.TravelPayouts.Token;
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("LevelTravelApiKey не задан — поиск туров пропущен");
                return null;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", $"token=\"{apiKey}\"");

            var url = "https://api.level.travel/search/enqueue";

            var parameters = new Dictionary<string, string>
            {
                ["from_city"] = fromCity,
                ["to_country"] = toCountry,
                ["adults"] = adults.ToString(),
                ["start_date"] = startDate.ToString("yyyy-MM-dd"),
                ["nights"] = $"{nightsMin}..{nightsMax}",
                ["search_type"] = "package"
            };

            var content = new FormUrlEncodedContent(parameters);

            var response = await _httpClient.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Level.Travel enqueue ошибка {Status}: {Json}", response.StatusCode, json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var requestId = doc.RootElement.GetProperty("request_id").GetString();

            _logger.LogInformation("Level.Travel поиск поставлен в очередь. request_id: {Id}", requestId);
            return requestId;
        }

        /// <summary>
        /// 2. Проверяем статус поиска
        /// </summary>
        public async Task<string> GetSearchStatusAsync(string requestId)
        {
            var url = $"https://api.level.travel/search/status?request_id={requestId}";
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var statusObj = doc.RootElement.GetProperty("status");

            // Берём первый завершенный статус (или "completed")
            foreach (var prop in statusObj.EnumerateObject())
            {
                if (prop.Value.GetString() == "completed" || prop.Value.GetString() == "cached")
                    return "completed";
            }

            return "pending";
        }

        /// <summary>
        /// 3. Получаем результаты (группированные отели/туры)
        /// </summary>
        public async Task<List<TourDeal>> GetGroupedHotelsAsync(string requestId)
        {
            var url = $"https://api.level.travel/search/get_grouped_hotels?request_id={requestId}";
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GetGroupedHotels ошибка: {Json}", json);
                return new List<TourDeal>();
            }

            var deals = new List<TourDeal>();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("hotels", out var hotels))
            {
                foreach (var hotel in hotels.EnumerateArray())
                {
                    if (!hotel.TryGetProperty("min_price", out var priceElem)) continue;

                    var deal = new TourDeal
                    {
                        Destination = hotel.GetProperty("hotel").GetProperty("city").GetString() ?? "",
                        HotelName = hotel.GetProperty("hotel").GetProperty("name").GetString() ?? "",
                        Stars = hotel.GetProperty("hotel").GetProperty("stars").GetInt32(),
                        MealType = "AI", // можно парсить из offers
                        DurationNights = 7, // берём среднее или из параметров
                        TotalPrice = priceElem.GetDecimal(),
                        DepartureDate = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"),
                        AffiliateLink = hotel.GetProperty("hotel").GetProperty("link").GetString() ?? ""
                    };

                    // Добавляем маркер и subid
                    if (!string.IsNullOrEmpty(deal.AffiliateLink))
                    {
                        deal.AffiliateLink += deal.AffiliateLink.Contains("?") ? "&" : "?";
                        deal.AffiliateLink += $"marker={_settings.Get("Marker")}&subid={_settings.Get("SubId")}";
                    }

                    deals.Add(deal);
                }
            }

            return deals;
        }
    }
}
