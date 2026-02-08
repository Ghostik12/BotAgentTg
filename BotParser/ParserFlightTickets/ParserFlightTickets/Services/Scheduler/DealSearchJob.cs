using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParserFlightTickets.Config;
using ParserFlightTickets.Services.Api;
using ParserFlightTickets.Services.Telegram;
using Quartz;
using System;
using System.Collections.Generic;
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
            _logger.LogInformation("DealSearchJob запущен в {Time}", DateTime.UtcNow);

            var config = _serviceProvider.GetRequiredService<BotConfig>();
            var apiService = _serviceProvider.GetRequiredService<TravelPayoutsService>();
            var publisher = _serviceProvider.GetRequiredService<TelegramPublisher>();

            var origins = config.SearchSettings.RussianCities;
            var destinations = config.SearchSettings.PopularDestinations;
            var priorityOrigins = config.SearchSettings.PriorityRussianCities;
            var priorityDestinations = config.SearchSettings.PriorityPopularDestinations;

            int published = 0;
            var publishedHashes = new HashSet<string>(); // для простого антидубликата (пока в памяти)

            var random = new Random();
            string origin, dest;

            // 80% шанс на приоритетную пару
            if (random.NextDouble() < 0.8 && priorityOrigins.Any() && priorityDestinations.Any())
            {
                origin = priorityOrigins[random.Next(priorityOrigins.Count)];
                dest = priorityDestinations[random.Next(priorityDestinations.Count)];
            }
            else
            {
                origin = origins[random.Next(origins.Count)];
                dest = destinations[random.Next(destinations.Count)];
            }

            if (origin == dest) return;
            try 
            { 
            // Запрос
            var deals = await apiService.SearchOneWayByPriceRangeAsync(
                origin, dest,
                minPrice: config.SearchSettings.MinPrice,
                maxPrice: config.SearchSettings.MaxPrice,
                directOnly: false,          // можно true, если хочешь только прямые
                limit: 5                    // берём до 5 вариантов, потом выберем лучший
            );

            if (deals == null || !deals.Any()) return;

            // Берём самый дешёвый (или рандомный)
            var bestDeal = deals.OrderBy(d => d.Price).First();

                // Получаем красивые названия городов (можно сделать словарь или API, пока заглушка)
                string originName = GetCityName(origin);       // например "Москва"
                string destName = GetCityName(bestDeal.Destination); // например "Пхукет"

                // Форматируем дату
                string dateFormatted = "ближайшая";
                if (!string.IsNullOrEmpty(bestDeal.DepartureDate) && DateTime.TryParse(bestDeal.DepartureDate.Split('T')[0], out DateTime depDate))
                {
                    dateFormatted = depDate.ToString("d MMMM", new System.Globalization.CultureInfo("ru-RU"));
                    // → "9 февраля"
                }

                // Цена с пробелами
                string priceFormatted = bestDeal.Price.ToString("N0", new System.Globalization.CultureInfo("ru-RU")); // → "25 900"

                // Авиакомпания (если пусто — заглушка)
                string airline = string.IsNullOrEmpty(bestDeal.Airline) ? "Аэрофлот" : bestDeal.Airline;
                
                // Формируем текст
                string postText = $"✈️ Улететь в {destName} из {originName} можно всего за {priceFormatted} рублей ({bestDeal.AffiliateLink}). " +
                                  $"Прямой перелет с багажом {dateFormatted} от авиакомпании {airline} ✈️\n\n" +
                                  $"Билеты берем по [ссылке]({bestDeal.AffiliateLink})";

                // Публикация
                string imageUrl = GetPlaceholderImage();

                await publisher.PublishToChannelAsync(postText, imageUrl);

                _logger.LogInformation("Опубликован one-way билет: {Origin}-{Dest} за {Price}₽", origin, bestDeal.Destination, bestDeal.Price);

            published++;

                    await Task.Delay(2000); // пауза между постами
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке {Origin}-{Dest}", origin, dest);
            }

            _logger.LogInformation("DealSearchJob завершён. Опубликовано: {Published}", published);
        }

        private string GetPlaceholderImage()
        {
            int rnd = new Random().Next(100, 999);
            return $"https://picsum.photos/seed/deal{rnd}/800/600";
        }

        private string GetCityName(string iataCode)
        {
            var cityMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "MOW", "Москвы" },
        { "LED", "Санкт-Петербурга" },
        { "SVX", "Екатеринбурга" },
        { "KZN", "Казани" },
        { "OVB", "Новосибирска" },
        { "BKK", "Бангкок" },
        { "HKT", "Пхукет" },
        { "DXB", "Дубай" },
        { "IST", "Стамбул" },
        { "DEL", "Дели" },
        { "TBS", "Тбилиси" },
        { "PAR", "Париж" },
        // добавляй другие по мере необходимости
    };

            return cityMap.TryGetValue(iataCode, out var name) ? name : iataCode;
        }
    }
}
