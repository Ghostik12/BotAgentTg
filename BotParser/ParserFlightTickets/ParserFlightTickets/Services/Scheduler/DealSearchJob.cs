using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParserFlightTickets.Config;
using ParserFlightTickets.Services.Api;
using ParserFlightTickets.Services.Data;
using ParserFlightTickets.Services.Telegram;
using Quartz;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ParserFlightTickets.Services.Scheduler
{
    public class DealSearchJob : IJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DealSearchJob> _logger;

        public DealSearchJob(IServiceProvider serviceProvider, ILogger<DealSearchJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var settings = _serviceProvider.GetRequiredService<SettingsService>();
            var apiService = _serviceProvider.GetRequiredService<TravelPayoutsService>();
            var publisher = _serviceProvider.GetRequiredService<TelegramPublisher>();

            // 1. Проверки перед запуском
            if (settings.IsNightPauseActive())
            {
                _logger.LogInformation("Ночная пауза активна — пропускаем");
                return;
            }

            if (!settings.CanPublishToday("flight"))
            {
                _logger.LogInformation("Лимит постов за день достигнут");
                return;
            }

            var origins = settings.GetList("Flights_DepartureCities");
            var destinations = settings.GetList("Flights_Destinations");
            var priorityOrigins = settings.GetList("Flights_PriorityDeparture");
            var priorityDestinations = settings.GetList("Flights_PriorityDestinations");

            if (!origins.Any() || !destinations.Any())
            {
                _logger.LogWarning("Нет городов для поиска рейсов");
                return;
            }

            var random = new Random();
            string origin, dest;

            bool canUsePriority = priorityOrigins.Any() && priorityDestinations.Any();
            if (canUsePriority && random.NextDouble() < 0.8)
            {
                origin = priorityOrigins[random.Next(priorityOrigins.Count)];
                dest = priorityDestinations[random.Next(priorityDestinations.Count)];
            }
            else
            {
                origin = origins[random.Next(origins.Count)];
                dest = destinations[random.Next(destinations.Count)];
            }

            if (origin == dest)
            {
                _logger.LogDebug("Пропущен маршрут в один город");
                return;
            }

            // 2. Настройки фильтров
            int maxTransfers = settings.GetInt("Flights_MaxTransfers");
            string depTime = settings.Get("Flights_DepartureTime");
            int adults = settings.GetInt("Flights_Adults");
            int minDays = settings.GetInt("Flights_MinDateDays");
            int maxDays = settings.GetInt("Flights_MaxDateDays");
            var specificDates = settings.GetList("Flights_SpecificDates");
            var includeAirlines = settings.GetList("Flights_IncludeAirlines");
            var excludeAirlines = settings.GetList("Flights_ExcludeAirlines");

            // 3. Выбираем дату вылета
            DateTime? targetDate = null;

            if (specificDates.Any())
            {
                // Если указаны конкретные даты или "weekends"/"holidays"
                var possibleDates = new List<DateTime>();
                var today = DateTime.Today;

                foreach (var dateStrs in specificDates)
                {
                    if (dateStrs == "weekends")
                    {
                        for (int d = minDays; d <= maxDays; d++)
                        {
                            var dts = today.AddDays(d);
                            if (dts.DayOfWeek == DayOfWeek.Saturday || dts.DayOfWeek == DayOfWeek.Sunday)
                                possibleDates.Add(dts);
                        }
                    }
                    else if (dateStrs == "holidays")
                    {
                        // Здесь можно добавить список праздников, пока пропустим или добавим вручную
                        continue;
                    }
                    else if (DateTime.TryParse(dateStrs, out DateTime exactDate))
                    {
                        if (exactDate >= today.AddDays(minDays) && exactDate <= today.AddDays(maxDays))
                            possibleDates.Add(exactDate);
                    }
                }

                if (possibleDates.Any())
                {
                    targetDate = possibleDates[random.Next(possibleDates.Count)];
                }
            }

            // Если конкретной даты нет — берём случайную в диапазоне
            if (!targetDate.HasValue)
            {
                int randomDays = random.Next(minDays, maxDays + 1);
                targetDate = DateTime.Today.AddDays(randomDays);
            }

            string departureMonth = targetDate.Value.ToString("yyyy-MM");

            // 4. Запрос к API
            var deals = await apiService.SearchOneWayPricesForDatesAsync(
                origin,
                dest,
                departureMonth: departureMonth,
                directOnly: maxTransfers == 0,
                limit: 10
            );

            if (deals?.Any() != true)
            {
                _logger.LogInformation("Нет предложений для {Origin} → {Dest}", origin, dest);
                return;
            }

            // 5. Применяем фильтры
            var filteredDeals = deals
                .Where(d =>
                {
                    // Пересадки
                    if (d.Transfers > maxTransfers) return false;

                    // Время вылета
                    if (depTime != "any")
                    {
                        if (!DateTime.TryParse(d.DepartureDate, out var depDt)) return false;
                        int hour = depDt.Hour;

                        if (depTime == "morning" && (hour < 6 || hour > 11)) return false;
                        if (depTime == "day" && (hour < 12 || hour > 17)) return false;
                        if (depTime == "evening" && (hour < 18 || hour > 23) && (hour > 5)) return false;
                    }

                    // Авиакомпании
                    if (includeAirlines.Any() && !includeAirlines.Contains(d.Airline ?? "")) return false;
                    if (excludeAirlines.Contains(d.Airline ?? "")) return false;

                    // Кол-во человек — в запросе уже учтено (adults)
                    return true;
                })
                .OrderBy(d => d.Price)
                .ToList();

            if (!filteredDeals.Any()) return;

            // 6. Берём лучший (или несколько, если нужно)
            var best = filteredDeals.First();

            // 7. Формируем пост
            string priceStr = best.Price.ToString("N0", new System.Globalization.CultureInfo("ru-RU"));
            string dateStr = DateTime.TryParse(best.DepartureDate, out var dt)
                ? dt.ToString("d MMMM", new System.Globalization.CultureInfo("ru-RU"))
                : "ближайшее время";

            string airlineName = GetAirlineName(best.Airline);
            string fullDest = GetCityName(dest);
            string fullOrigin = GetCityName(origin);
            string postText =
                $"✈️ Улететь в {fullDest} из {fullOrigin} можно всего за <a href=\"{best.AffiliateLink}\"><b>{priceStr} рублей</b></a>. " +
                $"Перелет с багажом {dateStr} от авиакомпании {airlineName} ✈️\n\n" +
                $"<a href=\"{best.AffiliateLink}\">Билеты</a>";

            //string imageUrl = GetPlaceholderImage();
            byte[] imageBytes = ImageHelper.AddTextToLocalImage(
    text: $"{priceStr} ₽\n{dateStr}",
    cityName: fullDest // или originName
);

            if (imageBytes != null)
            {
                await publisher.PublishToChannelAsync(postText, Convert.ToBase64String(imageBytes));
            }
            else
            {
                // Fallback — только текст
                await publisher.PublishToChannelAsync(postText);
            }

            // 8. Записываем в историю
            string hash = $"flight-{origin}-{dest}-{best.DepartureDate}-{best.Price}";
            settings.AddPublished(hash, "flight");

            _logger.LogInformation("Опубликован рейс: {Origin} → {Dest} за {Price}₽", origin, dest, best.Price);
        }

        private string GetCityName(string iataCode)
        {
            var cityMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Города вылета (Россия)
        { "MOW", "Москвы" },
        { "LED", "Санкт-Петербурга" },
        { "SVX", "Екатеринбурга" },
        { "KZN", "Казани" },
        { "OVB", "Новосибирска" },
        { "AER", "Сочи" },
        { "KRR", "Краснодара" },
        { "ROV", "Ростова-на-Дону" },
        { "UFA", "Уфы" },
        { "KJA", "Красноярска" },
        { "VVO", "Владивостока" },
        { "PKC", "Петропавловска-Камчатского" },
        { "SVO", "Москвы" }, // Шереметьево (если по аэропорту)
        { "DME", "Москвы" },
        { "VKO", "Москвы" },

        // Популярные направления (прилёта)
        { "BKK", "Бангкок" },
        { "HKT", "Пхукет" },
        { "DXB", "Дубай" },
        { "SHJ", "Шарджу" },
        { "IST", "Стамбул" },
        { "AYT", "Анталию" },
        { "DEL", "Дели" },
        { "GOI", "Гоа" },
        { "TBS", "Тбилиси" },
        { "TIV", "Тиват" },
        { "BUD", "Будапешт" },
        { "PAR", "Париж" },
        { "BCN", "Барселону" },
        { "ROM", "Рим" },
        { "MIL", "Милан" },
        { "ATH", "Афины" },
        { "CAI", "Каир" },
        { "SSH", "Шарм-эль-Шейх" },
        { "HRG", "Хургаду" },
        { "CMB", "Коломбо" },
        { "MLE", "Мале" },
        { "MRU", "Маврикий" },
        { "BAH", "Бахрейн" },
        { "DOH", "Доху" },
        { "JED", "Джидду" },

        // Европа и другие
        { "PRG", "Прагу" },
        { "VIE", "Вену" },
        { "BER", "Берлин" },
        { "AMS", "Амстердам" },
        { "LON", "Лондон" },
        { "MAD", "Мадрид" },
        { "LIS", "Лиссабон" },
        { "ZRH", "Цюрих" },
        { "GVA", "Женеву" },
        { "OSL", "Осло" },
        { "HEL", "Хельсинки" },

        // Азия и дальние
        { "BALI", "Бали" }, // не IATA, но часто ищут как BPN/DPS
        { "DPS", "Денпасар (Бали)" },
        { "CNX", "Чиангмай" },
        { "KTM", "Катманду" },
        { "HAN", "Ханой" },
        { "SGN", "Хошимин" },
        { "PNH", "Пномпень" },
        { "RGN", "Янгон" }
    };

            return cityMap.TryGetValue(iataCode, out var name) ? name : iataCode;
        }

        private string GetAirlineName(string? airlineCode)
        {
            if (string.IsNullOrEmpty(airlineCode))
                return "надёжный перевозчик";

            var airlineMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "SU", "Аэрофлот" },
        { "S7", "S7 Airlines" },
        { "DP", "Победа" },
        { "FV", "Россия" },
        { "N4", "Nordwind" },
        { "UT", "ЮТэйр" },
        { "WZ", "Red Wings" },
        { "ZF", "Azur Air" },
        { "RL", "Royal Flight" },
        { "EO", "Pegas Fly" },
        { "U6", "Уральские авиалинии" },
        { "HY", "Uzbekistan Airways" },
        { "KC", "Air Astana" },
        { "TK", "Turkish Airlines" },
        { "EK", "Emirates" },
        { "QR", "Qatar Airways" },
        { "EY", "Etihad Airways" },
        { "GF", "Gulf Air" },
        { "WY", "Oman Air" },
        { "KU", "Kuwait Airways" },
        { "J9", "Jazeera Airways" },
        { "W5", "Mahann Air" },
        { "IR", "Iran Air" },
        { "6E", "IndiGo" },
        { "AI", "Air India" },
        { "UK", "Vistara" },
        { "TG", "Thai Airways" },
        { "PG", "Bangkok Airways" },
        { "DD", "Nok Air" },
        { "FD", "AirAsia" },
        { "AK", "AirAsia" },
        { "KE", "Korean Air" },
        { "OZ", "Asiana Airlines" },
        { "JL", "Japan Airlines" },
        { "NH", "All Nippon Airways" },
        { "SQ", "Singapore Airlines" },
        { "QF", "Qantas" },
        { "BA", "British Airways" },
        { "LH", "Lufthansa" },
        { "AF", "Air France" },
        { "KL", "KLM" },
        { "AY", "Finnair" },
        { "SK", "SAS" },
        { "OS", "Austrian Airlines" },
        { "LX", "Swiss" },
        { "BT", "airBaltic" },
        { "LO", "LOT Polish Airlines" },
        { "FR", "Ryanair" },
        { "U2", "easyJet" },
        { "VY", "Vueling" },
        { "IB", "Iberia" },
        { "TP", "TAP Air Portugal" },
        { "EI", "Aer Lingus" },
        { "2S", "Southwind Airlines"}
    };

            return airlineMap.TryGetValue(airlineCode, out var name) ? name : airlineCode;
        }
    }
}
