using System;
using System.Collections.Generic;
using System.Text;

namespace VkBotParser.Models
{
    public class SentYoudoOrder
    {
        public int Id { get; set; }
        public long TaskId { get; set; } // Из /t14257992
        public long UserTelegramId { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
