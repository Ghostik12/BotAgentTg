using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ParserFlightTickets.Services.Api;
using ParserFlightTickets.Services.Data;
using ParserFlightTickets.Services.Telegram;
using Quartz;
using System;
using System.Collections.Generic;
using System.Text;

namespace ParserFlightTickets.Services.Scheduler
{
    public class TourSearchJob : IJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TourSearchJob> _logger;

        public TourSearchJob(IServiceProvider serviceProvider, ILogger<TourSearchJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var settings = _serviceProvider.GetRequiredService<SettingsService>();
            var levelTravel = _serviceProvider.GetRequiredService<LevelTravelService>();
            var publisher = _serviceProvider.GetRequiredService<TelegramPublisher>();

            if (settings.IsNightPauseActive() || !settings.CanPublishToday("tour"))
            {
                _logger.LogInformation("Публикация туров пропущена (пауза или лимит)");
                return;
            }

            var destinations = settings.GetList("Tours_Destinations");
            if (!destinations.Any())
            {
                _logger.LogWarning("Нет направлений для туров");
                return;
            }

            var random = new Random();
            string dest = destinations[random.Next(destinations.Count)];

            int minDays = settings.GetInt("Tours_MinDateDays", 3);
            int maxDays = settings.GetInt("Tours_MaxDateDays", 120);
            int nightsMin = settings.GetInt("Tours_MinDuration", 5);
            int nightsMax = settings.GetInt("Tours_MaxDuration", 14);

            int randomDays = random.Next(minDays, maxDays + 1);
            var departure = DateTime.Today.AddDays(randomDays);

            var deals = await levelTravel.EnqueueSearchAsync(dest, departure, nightsMin, nightsMax);

            if (deals?.Any() != true)
            {
                _logger.LogInformation("Туры не найдены для {Dest}", dest);
                return;
            }

            var filtered = deals
                .Where(d => d.TotalPrice >= settings.GetDecimal("Tours_MinPrice") &&
                            d.TotalPrice <= settings.GetDecimal("Tours_MaxPrice") &&
                            d.Stars >= settings.GetInt("Tours_MinStars", 3) &&
                            d.Transfers <= settings.GetInt("Tours_MaxTransfers", 2))
                .OrderBy(d => d.TotalPrice)
                .Take(3);

            if (!filtered.Any()) return;

            foreach (var deal in filtered)
            {
                string priceStr = deal.TotalPrice.ToString("N0", new System.Globalization.CultureInfo("ru-RU"));

                string postText =
                    $"🌴 Тур в {deal.Destination} за <a href=\"{deal.AffiliateLink}\"><b>{priceStr} ₽</b></a>\n" +
                    $"Отель {deal.HotelName}, питание {deal.MealType}\n" +
                    $"Продолжительность: {deal.DurationNights} ночей\n" +
                    $"Дата вылета: {deal.DepartureDate}\n" +
                    $"Пересадки: {deal.Transfers}\n\n" +
                    $"<a href=\"{deal.AffiliateLink}\">Забронировать</a>";

                string imageUrl = $"https://picsum.photos/seed/tour{random.Next(1000)}/800/600";

                await publisher.PublishToChannelAsync(postText, imageUrl);

                await Task.Delay(2000);
            }

            _logger.LogInformation("TourSearchJob завершён. Опубликовано: {Count}", filtered.Count());
        }
    }
}
