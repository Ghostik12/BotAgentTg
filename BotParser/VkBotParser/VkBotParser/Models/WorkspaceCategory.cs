using System;
using System.Collections.Generic;
using System.Text;

namespace VkBotParser.Models
{
    public class WorkspaceCategory
    {
        public int Id { get; set; }
        public long UserId { get; set; }
        public int CategorySlug { get; set; } = 1;   // "" = все тендеры, иначе "seo", "web-development" и т.д.
        public string Name { get; set; } = null!;
        public string NotificationInterval { get; set; } = "off";
    }
}
