using System;
using System.Collections.Generic;
using System.Text;

namespace VkBotParser.Models
{
    public class YoudoCategory
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; } = null!;
        public long UserId { get; set; }
        public User? User { get; set; }
        public string NotificationInterval { get; set; } = "off";
    }
}
