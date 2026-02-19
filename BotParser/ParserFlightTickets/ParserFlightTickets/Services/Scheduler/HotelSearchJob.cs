using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParserFlightTickets.Services.Api;
using ParserFlightTickets.Services.Data;
using ParserFlightTickets.Services.Telegram;
using Quartz;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ParserFlightTickets.Services.Scheduler
{
    public class HotelSearchJob : IJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HotelSearchJob> _logger;

        public HotelSearchJob(IServiceProvider serviceProvider, ILogger<HotelSearchJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("HotelSearchJob запущен в {Time}", DateTime.UtcNow);

            var settings = _serviceProvider.GetRequiredService<SettingsService>();
            var ostrovok = _serviceProvider.GetRequiredService<OstrovokService>();
            var publisher = _serviceProvider.GetRequiredService<TelegramPublisher>();

            if (settings.IsNightPauseActive() || !settings.CanPublishToday("hotel"))
            {
                _logger.LogInformation("Публикация отелей пропущена (пауза или лимит)");
                return;
            }

            var destinations = settings.GetList("HotelDestinations");
            if (!destinations.Any())
            {
                _logger.LogWarning("Нет городов для поиска отелей");
                return;
            }

            var random = new Random();
            string city = destinations[random.Next(destinations.Count)];

            // Даты заезда/выезда — ближайшие +3..+120 дней, 5–14 ночей
            int nights = random.Next(5, 15);
            var checkIn = DateTime.Now.AddDays(3 + random.Next(117));
            var checkOut = checkIn.AddDays(nights);

            var deals = await ostrovok.SearchHotelsAsync(city, checkIn, checkOut);

            if (deals?.Any() != true)
            {
                _logger.LogInformation("Отели не найдены для {City}", city);
                return;
            }

            // Фильтруем по настройкам
            var filtered = deals
                .Where(d => d.PricePerNight >= settings.GetInt("MinPriceHotels") &&
                            d.PricePerNight <= settings.GetInt("MaxPriceHotels") &&
                            d.Stars >= settings.GetInt("MinStarsHotels", 3) &&
                            d.Rating >= decimal.Parse(settings.Get("MinRatingHotels", "7.5")))
                .OrderBy(d => d.PricePerNight)
                .Take(3);  // берём топ-3

            if (!filtered.Any()) return;

            foreach (var deal in filtered)
            {
                string priceStr = deal.PricePerNight.ToString("N0", new CultureInfo("ru-RU"));

                string postText =
                    $"🏨 Отличный отель в {deal.CityCode} всего за <a href=\"{deal.AffiliateLink}\"><b>{priceStr} ₽/ночь</b></a>\n" +
                    $"Звёзды: {deal.Stars} ★   Рейтинг: {deal.Rating:F1}\n" +
                    $"Питание: {deal.MealType}\n" +
                    $"Даты: {deal.CheckIn:dd.MM} – {deal.CheckOut:dd.MM} ({(deal.CheckOut - deal.CheckIn).Days} ночей)\n\n" +
                    $"<a href=\"{deal.AffiliateLink}\">Забронировать</a>";

                string imageUrl = $"https://picsum.photos/seed/hotel{random.Next(1000)}/800/600";

                await publisher.PublishToChannelAsync(postText, imageUrl);

                await Task.Delay(2000); // пауза между постами
            }

            _logger.LogInformation("HotelSearchJob завершён. Опубликовано: {Count}", filtered.Count());
        }
    }
}
