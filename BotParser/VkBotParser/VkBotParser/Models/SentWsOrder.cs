using System;
using System.Collections.Generic;
using System.Text;

namespace VkBotParser.Models
{
    public class SentWsOrder
    {
        public int Id { get; set; }
        public long TenderId { get; set; }
        public long UserTelegramId { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
