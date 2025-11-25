using System;
using System.Collections.Generic;
using System.Text;

namespace BotParser.Models
{
    public class UserKeywordFilter
    {
        public int Id { get; set; }
        public long UserId { get; set; }

        public string Platform { get; set; } = null!;     // "workspace", "kwork", "fl" и т.д.
        public int CategoryId { get; set; }               // твой int (2, 3, 724 и т.д.)

        public string Word { get; set; } = null!;         // например: "битрикс"
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}
