using System;
using System.Collections.Generic;
using System.Text;

namespace BotParser.Models
{
    public class FrCategory
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; } = null!;
        public long UserId { get; set; }
        public string NotificationInterval { get; set; } = "off";
    }
}
