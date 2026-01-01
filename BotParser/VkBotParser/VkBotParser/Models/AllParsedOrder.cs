using System;
using System.Collections.Generic;
using System.Text;

namespace VkBotParser.Models
{
    public class AllParsedOrder
    {
        public long Id { get; set; }
        public string Platform { get; set; } = null!;       // "kwork", "fl", "freelance", "workspace", "youdo"
        public long ExternalId { get; set; }                // ID заказа на бирже
        public string Title { get; set; } = null!;
        public string? Budget { get; set; }
        public string Url { get; set; } = null!;
        public string? Category { get; set; }
        public DateTime PublishedAt { get; set; }
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    }
}
