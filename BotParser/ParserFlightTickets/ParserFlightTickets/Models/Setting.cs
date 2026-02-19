using System;
using System.Collections.Generic;
using System.Text;

namespace ParserFlightTickets.Models
{
    public class Setting
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;  // "MinPrice", "Marker", "FlightsTemplate" и т.д.
        public string Value { get; set; } = string.Empty;  // "5000", "459589" или JSON для списков
    }
}
