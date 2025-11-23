using System;
using System.Collections.Generic;
using System.Text;

namespace BotParser.Models
{
    public class SentFlOrder
    {
        public int Id { get; set; }
        public long ProjectId { get; set; } // ID проекта на FL.ru
        public long UserTelegramId { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
