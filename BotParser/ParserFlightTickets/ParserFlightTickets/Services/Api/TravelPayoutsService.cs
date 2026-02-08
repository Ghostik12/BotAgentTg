using Microsoft.Extensions.Logging;
using ParserFlightTickets.Config;
using ParserFlightTickets.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ParserFlightTickets.Services.Api
{
    public class TravelPayoutsService
    {
        private readonly HttpClient _httpClient;
        private readonly BotConfig _config;
        private readonly ILogger<TravelPayoutsService> _logger;

        public TravelPayoutsService(BotConfig config, ILogger<TravelPayoutsService> logger)
        {
            _config = config;
            _logger = logger;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://api.travelpayouts.com/"),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>
        /// Поиск самых дешёвых билетов по маршруту за месяц (cheap prices calendar)
        /// </summary>
        /// <param name="origin">IATA код города вылета (MOW, LED и т.д.)</param>
        /// <param name="destination">IATA код города прилёта</param>
        /// <param name="departDate">Дата вылета в формате YYYY-MM (или null для ближайших)</param>
        /// <returns>Список найденных предложений или null при ошибке</returns>
        public async Task<List<FlightDeal>?> SearchOneWayByPriceRangeAsync(
    string origin,
    string destination,
    int minPrice = 0,
    int maxPrice = 30000,
    bool directOnly = true,
    int limit = 10,
    int page = 1)
        {
            try
            {
                string url = "aviasales/v3/search_by_price_range" +
                             $"?origin={origin}" +
                             $"&destination={destination}" +
                             $"&value_min={minPrice}" +
                             $"&value_max={maxPrice}" +
                             $"&one_way=true" +                  // строго в одну сторону
                             $"&direct={directOnly.ToString().ToLower()}" +
                             $"&locale=ru" +
                             $"&currency=rub" +
                             $"&market=ru" +
                             $"&limit={limit}" +
                             $"&page={page}" +
                             $"&token={_config.TravelPayouts.Token}";

                _logger.LogInformation("Запрос one-way по цене: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("API вернул {Status}: {Error}", response.StatusCode, errorContent);
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Ответ API one-way:\n{Json}", json);

                var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("success", out var success) ||
                    !success.GetBoolean())
                {
                    _logger.LogWarning("success != true");
                    return null;
                }

                if (!doc.RootElement.TryGetProperty("data", out var dataElem) ||
                    dataElem.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogInformation("data пустой или не массив");
                    return null;
                }

                var deals = new List<FlightDeal>();

                foreach (var item in dataElem.EnumerateArray())
                {
                    if (!item.TryGetProperty("price", out var priceElem) ||
                        !priceElem.TryGetInt32(out int price))
                        continue;

                    var deal = new FlightDeal
                    {
                        Origin = item.TryGetProperty("origin_code", out var o) ? o.GetString() ?? origin : origin,
                        Destination = item.TryGetProperty("destination_code", out var d) ? d.GetString() ?? destination : destination,
                        DepartureDate = item.TryGetProperty("departure_at", out var dep) ? dep.GetString() : null,
                        Price = price,
                        Airline = null, // в этом API нет airline
                        FlightNumber = null,
                        DurationMinutes = item.TryGetProperty("duration", out var dur) && dur.TryGetInt32(out int dm) ? dm : 0,
                        Transfers = item.TryGetProperty("transfers", out var tr) && tr.TryGetInt32(out int t) ? t : 0,
                        IsRoundTrip = false,
                        Adults = 1
                    };

                    // Ссылка из ответа (уже с параметрами, но без маркера)
                    string baseLink = item.TryGetProperty("link", out var linkElem) ? linkElem.GetString() ?? "" : "";

                    if (!string.IsNullOrEmpty(baseLink))
                    {
                        // Если ссылка относительная — делаем полную
                        if (baseLink.StartsWith("/"))
                            baseLink = "https://www.aviasales.ru" + baseLink;

                        // Пока используем как есть (потом можно через партнёрский API)
                        deal.AffiliateLink = baseLink + $"?marker={_config.TravelPayouts.Marker}&subid={_config.TravelPayouts.SubId}";
                    }
                    else
                    {
                        // Fallback на ручную генерацию
                        string ddmm = "";
                        if (DateTime.TryParse(deal.DepartureDate, out var dt))
                            ddmm = dt.ToString("ddMM");

                        deal.AffiliateLink = $"https://www.aviasales.ru/search/{origin}{ddmm}{destination}?marker={_config.TravelPayouts.Marker}&subid={_config.TravelPayouts.SubId}";
                    }

                    deals.Add(deal);
                }

                _logger.LogInformation("Найдено one-way предложений: {Count}", deals.Count);
                return deals.Any() ? deals : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в SearchOneWayByPriceRangeAsync");
                return null;
            }
        }

        private static string? GetStringOrNull(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop))
                return null;

            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.GetInt32().ToString(),     // если число — конвертируем в строку
                JsonValueKind.Null => null,
                _ => prop.ToString() // fallback: просто ToString() на случай чего-то странного
            };
        }
    }
}
