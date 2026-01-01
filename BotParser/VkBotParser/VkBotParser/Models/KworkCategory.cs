using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkBotParser.Models
{
    public class KworkCategory
    {
        public int Id { get; set; }
        public int CategoryId { get; set; } // ID на kwork.ru
        public string Name { get; set; } = null!;
        public long UserId { get; set; }
        public User? User { get; set; }
        public string NotificationInterval { get; set; } = "off";
    }
}
