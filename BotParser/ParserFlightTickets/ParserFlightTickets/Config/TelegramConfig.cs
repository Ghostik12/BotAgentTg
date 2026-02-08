using System;
using System.Collections.Generic;
using System.Text;

namespace ParserFlightTickets.Config
{
    public class TelegramConfig
    {
        public string Token { get; set; } = string.Empty;
        public long ChannelId { get; set; }
        public long AdminUserId { get; set; }
    }
}
