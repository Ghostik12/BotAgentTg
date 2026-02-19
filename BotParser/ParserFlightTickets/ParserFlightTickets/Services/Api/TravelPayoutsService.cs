using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParserFlightTickets.Config;
using ParserFlightTickets.Models;
using ParserFlightTickets.Services.Data;
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
        private readonly IServiceProvider _serviceProvider;

        public TravelPayoutsService(BotConfig config, ILogger<TravelPayoutsService> logger, IServiceProvider serviceProvider)
        {
            _config = config;
            _logger = logger;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://api.travelpayouts.com/"),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Поиск самых дешёвых билетов по маршруту за месяц (cheap prices calendar)
        /// </summary>
        /// <param name="origin">IATA код города вылета (MOW, LED и т.д.)</param>
        /// <param name="destination">IATA код города прилёта</param>
        /// <param name="departDate">Дата вылета в формате YYYY-MM (или null для ближайших)</param>
        /// <returns>Список найденных предложений или null при ошибке</returns>
        public async Task<List<FlightDeal>?> SearchOneWayPricesForDatesAsync(
    string origin,
    string destination,
    string? departureMonth = null,  // "2026-02" или "2026-02-10"
    bool directOnly = true,
    int limit = 10,
    int page = 1)
        {
            try
            {
                var settings = _serviceProvider.GetRequiredService<SettingsService>();
                string url = "aviasales/v3/prices_for_dates" +
                             $"?origin={origin}" +
                             $"&destination={destination}" +
                             $"&one_way=true" +  // строго в одну сторону
                             $"&currency=rub" +
                             $"&market=ru" +
                             $"&locale=ru" +
                             $"&sorting=price" +   // по цене
                             $"&direct={directOnly.ToString().ToLower()}" +
                             $"&limit={limit}" +
                             $"&page={page}" +
                             $"&token={_config.TravelPayouts.Token}";

                if (!string.IsNullOrEmpty(departureMonth))
                {
                    url += $"&departure_at={departureMonth}";
                }

                _logger.LogInformation("Запрос prices_for_dates (one-way): {Url}", url);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    string err = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("API error {Status}: {Error}", response.StatusCode, err);
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Ответ prices_for_dates:\n{Json}", json);

                var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
                {
                    _logger.LogWarning("success != true");
                    return null;
                }

                if (!doc.RootElement.TryGetProperty("data", out var dataElem) ||
                    dataElem.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogInformation("data пустой");
                    return null;
                }

                var deals = new List<FlightDeal>();

                foreach (var item in dataElem.EnumerateArray())
                {
                    if (!item.TryGetProperty("price", out var p) || !p.TryGetInt32(out int price))
                        continue;

                    var deal = new FlightDeal
                    {
                        Origin = item.TryGetProperty("origin", out var o) ? o.GetString() ?? origin : origin,
                        Destination = item.TryGetProperty("destination", out var d) ? d.GetString() ?? destination : destination,
                        DepartureDate = item.TryGetProperty("departure_at", out var dep) ? dep.GetString() : null,
                        Price = price,
                        Airline = item.TryGetProperty("airline", out var al) ? al.GetString() : null,          // ← вот оно!
                        FlightNumber = item.TryGetProperty("flight_number", out var fn) ? fn.GetString() : null,
                        DurationMinutes = item.TryGetProperty("duration", out var dur) && dur.TryGetInt32(out int dm) ? dm : 0,
                        Transfers = item.TryGetProperty("transfers", out var tr) && tr.TryGetInt32(out int t) ? t : 0,
                        IsRoundTrip = false,
                        Adults = 1
                    };

                    // Ссылка из API
                    string baseLink = item.TryGetProperty("link", out var lnk) ? lnk.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(baseLink))
                    {
                        if (baseLink.StartsWith("/"))
                            baseLink = "https://www.aviasales.ru" + baseLink;

                        deal.AffiliateLink = baseLink + (baseLink.Contains("?") ? "&" : "?") +
                                             $"marker={settings.Get("Marker")}&subid={settings.Get("SubId")}";
                    }
                    else
                    {
                        // fallback
                        string ddmm = "";
                        if (DateTime.TryParse(deal.DepartureDate?.Split('T')[0], out var dt))
                            ddmm = dt.ToString("ddMM");

                        deal.AffiliateLink = $"https://www.aviasales.ru/search/{origin}{ddmm}{destination}1?marker={settings.Get("Marker")}&subid={settings.Get("SubId")}";
                    }

                    deals.Add(deal);
                }

                _logger.LogInformation("Найдено one-way билетов: {Count}", deals.Count);
                return deals.Any() ? deals : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в SearchOneWayPricesForDatesAsync");
                return null;
            }
        }
    }
}
