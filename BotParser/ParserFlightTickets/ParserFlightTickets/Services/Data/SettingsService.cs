using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ParserFlightTickets.Config;
using ParserFlightTickets.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ParserFlightTickets.Services.Data
{
    public class SettingsService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<SettingsService> _logger;

        public SettingsService(AppDbContext db, ILogger<SettingsService> logger)
        {
            _db = db;
            _logger = logger;
            _db.Database.EnsureCreated();
            InitializeDefaultSettingsIfEmpty();
        }

        private void InitializeDefaultSettingsIfEmpty()
        {
            if (_db.Settings.Any()) return;

            // Общие
            Set("Admin1", "1451999567");
            Set("Marker", "459589");
            Set("SubId", "hiticketstg");
            Set("NightPauseFrom", "22:00");
            Set("NightPauseTo", "08:00");

            // Авиабилеты
            Set("Flights_MinPrice", "5000");
            Set("Flights_MaxPrice", "25000");
            Set("Flights_CheckIntervalMinutes", "60");
            Set("Flights_MaxPostsPerDay", "8");
            Set("Flights_Template", "✈️ Улететь в {dest} из {origin} можно всего за <a href=\"{link}\"><b>{price} рублей</b></a>. Перелет с багажом {date} от авиакомпании {airline} ✈️\n\n<a href=\"{link}\">Билеты</a>");
            SetList("Flights_DepartureCities", new List<string> { "MOW", "LED", "SVX", "KZN", "OVB", "AER", "ROV", "KRR", "UFA", "KJA" });
            SetList("Flights_Destinations", new List<string> { "BKK", "DXB", "IST", "DEL", "HKT", "AYT", "TBS", "TIV", "BUD", "PAR" });
            SetList("Flights_PriorityDeparture", new List<string> { "MOW", "LED" });
            SetList("Flights_PriorityDestinations", new List<string> { "BKK", "HKT" });
            SetList("Flights_BlacklistDestinations", new List<string>());
            Set("Flights_MaxTransfers", "2");
            Set("Flights_DepartureTime", "any"); // any, morning, day, evening
            Set("Flights_Adults", "1");
            Set("Flights_MinDateDays", "3");
            Set("Flights_MaxDateDays", "120");
            SetList("Flights_SpecificDates", new List<string>()); // JSON списка дат "2026-02-14" или "weekends"
            SetList("Flights_IncludeAirlines", new List<string>());
            SetList("Flights_ExcludeAirlines", new List<string>());

            // Отели
            Set("Hotels_MinPrice", "1500");
            Set("Hotels_MaxPrice", "8000");
            Set("Hotels_CheckIntervalMinutes", "120");
            Set("Hotels_MaxPostsPerDay", "5");
            Set("Hotels_Template", "🏨 Отель в {city} за <a href=\"{link}\"><b>{price} ₽/ночь</b></a>\nЗвёзды: {stars} ★ Рейтинг: {rating}\nПитание: {meal}\nДаты: {checkin} – {checkout}\n\n<a href=\"{link}\">Забронировать</a>");
            SetList("Hotels_Destinations", new List<string> { "BKK", "HKT", "DXB", "IST" });
            Set("Hotels_MinStars", "3");
            Set("Hotels_MinRating", "7.5");
            Set("Hotels_MealType", "all_included");
            SetList("Hotels_Amenities", new List<string> { "pool", "spa", "wifi", "beach" });
            Set("Hotels_MaxDistanceCenter", "5");
            Set("Hotels_MaxDistanceBeach", "1");
            Set("Hotels_MinDateDays", "3");
            Set("Hotels_MaxDateDays", "120");
            Set("Hotels_Adults", "2");

            // Туры
            Set("Tours_MinPrice", "5000");
            Set("Tours_MaxPrice", "500000");
            Set("Tours_CheckIntervalMinutes", "180");
            Set("Tours_MaxPostsPerDay", "4");
            Set("Tours_Template", "🌴 Тур в {dest} за <a href=\"{link}\"><b>{price} ₽</b></a>\nОтель {hotel}, питание {meal}\nПродолжительность: {duration} ночей\nДата: {departure}\nПересадки: {transfers}\n\n<a href=\"{link}\">Забронировать</a>");
            SetList("Tours_Destinations", new List<string> { "AYT", "HKT", "BKK" });
            Set("Tours_MinStars", "3");
            Set("Tours_MealType", "all_included");
            Set("Tours_Type", "beach");
            Set("Tours_MinDuration", "5");
            Set("Tours_MaxDuration", "14");
            Set("Tours_MaxTransfers", "2");
            Set("Tours_MinDateDays", "3");
            Set("Tours_MaxDateDays", "120");
            Set("Tours_Adults", "2");
        }

        public long GetLong(string key, long defaultValue = 0)
        {
            return long.TryParse(Get(key), out long val) ? val : defaultValue;
        }

        public string Get(string key, string defaultVal = "") =>
        _db.Settings.FirstOrDefault(s => s.Key == key)?.Value ?? defaultVal;

        public void Set(string key, string value)
        {
            var s = _db.Settings.FirstOrDefault(x => x.Key == key) ?? new Setting { Key = key };
            s.Value = value;
            if (s.Id == 0) _db.Settings.Add(s);
            _db.SaveChanges();
            _logger.LogInformation("Настройка изменена: {Key} → {Value}", key, value);
        }

        public int GetInt(string key, int def = 0) => int.TryParse(Get(key), out int v) ? v : def;
        public bool GetBool(string key, bool def = false) => bool.TryParse(Get(key), out bool v) ? v : def;

        public List<string> GetList(string key)
        {
            string json = Get(key);
            return string.IsNullOrEmpty(json) ? new() : JsonConvert.DeserializeObject<List<string>>(json) ?? new();
        }

        public void SetList(string key, List<string> list) => Set(key, JsonConvert.SerializeObject(list));

        // Специфические методы
        public bool IsNightPauseActive()
        {
            var fromStr = Get("NightPauseFrom", "22:00");
            var toStr = Get("NightPauseTo", "08:00");

            if (!TimeSpan.TryParse(fromStr, out var from) || !TimeSpan.TryParse(toStr, out var to))
                return false;

            var now = DateTime.UtcNow.TimeOfDay;

            // Если пауза через полночь (22:00–08:00)
            if (to < from)
            {
                return now >= from || now <= to;
            }

            return now >= from && now <= to;
        }

        public bool CanPublishToday(string type = "flight")
        {
            int max = GetInt("MaxPostsPerDay", 8);
            int today = _db.PublishedDeals.Count(p => p.Type == type && p.PublishedAt.Date == DateTime.UtcNow.Date);
            return today < max;
        }

        public void AddPublished(string hash, string type = "flight")
        {
            _db.PublishedDeals.Add(new PublishedDeal
            {
                Hash = hash,
                Type = type,
                PublishedAt = DateTime.UtcNow
            });
            _db.SaveChanges();
        }
        public decimal GetDecimal(string key, int def = 0) => decimal.TryParse(Get(key), out decimal v) ? v : def;
    }
}
