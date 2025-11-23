using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotParser.Models
{
    public class User
    {
        public long Id { get; set; } // Telegram ID
        public string? Username { get; set; }
        public List<KworkCategory> SelectedCategories { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CurrentStep { get; set; }
    }
}
