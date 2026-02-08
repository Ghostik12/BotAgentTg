using System;
using System.Collections.Generic;
using System.Text;

namespace ParserFlightTickets.Config
{
    public class BotConfig
    {
        public TelegramConfig Telegram { get; set; } = new();
        public TravelPayoutsConfig TravelPayouts { get; set; } = new();
        public SearchSettingsConfig SearchSettings { get; set; } = new();
    }
}
