using System;
using System.Collections.Generic;
using System.Text;

namespace ParserFlightTickets.Models
{
    public class PublishedDeal
    {
        public int Id { get; set; }
        public string Hash { get; set; } = string.Empty;  // "MOW-DXB-2026-02-10-45200"
        public DateTime PublishedAt { get; set; }
        public string Type { get; set; } = "flight";  // flight, hotel, tour
    }
}
