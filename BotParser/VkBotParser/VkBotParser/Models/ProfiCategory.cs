using System;
using System.Collections.Generic;
using System.Text;

namespace VkBotParser.Models
{
    public class ProfiCategory
    {
        public int Id { get; set; }
        public long UserId { get; set; }
        public string SearchQuery { get; set; } = null!;     // "разработка сайтов", "битрикс", "telegram бот"
        public string Name { get; set; } = null!;            // "Мои сайты на Битрикс" (для удобства)
        public string NotificationInterval { get; set; } = "off";
    }
}
