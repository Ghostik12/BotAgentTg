using System;
using System.Collections.Generic;
using System.Text;

namespace VkBotParser.Models
{
    public class SentProfiOrder
    {
        public int Id { get; set; }
        public long OrderId { get; set; }             // ID заказа на Profi.ru
        public long UserTelegramId { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
