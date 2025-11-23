using System;
using System.Collections.Generic;
using System.Text;

namespace BotParser.Models
{
    public class FlCategory
    {
        public int Id { get; set; }
        public int CategoryId { get; set; } // ID на FL.ru (1 = сайты, 2 = дизайн и т.д.)
        public string Name { get; set; } = null!;
        public string NotificationInterval { get; set; } = "off"; // off, instant, 15min, hour, day

        public long UserId { get; set; }
        public User? User { get; set; }
    }
}
