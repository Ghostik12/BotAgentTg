using System;
using System.Collections.Generic;
using System.Text;

namespace VkBotParser.Models
{
    public class SentOrder
    {
        public int Id { get; set; }
        public long ProjectId { get; set; }
        public long UserTelegramId { get; set; }
        public DateTime SentAt { get; set; }
    }
}
