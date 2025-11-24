using System;
using System.Collections.Generic;
using System.Text;

namespace BotParser.Models
{
    public class UserKeywordFilter
    {
        public int Id { get; set; }
        public long UserId { get; set; }
        public string Keyword { get; set; } = null!;        // например: "битрикс", "laravel", "сайт под ключ", "telegram bot"
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
