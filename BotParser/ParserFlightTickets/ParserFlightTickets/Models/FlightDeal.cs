using System;
using System.Collections.Generic;
using System.Text;

namespace ParserFlightTickets.Models
{
    public class FlightDeal
    {
        public string Origin { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string DepartureDate { get; set; } = string.Empty;
        public int Price { get; set; }
        public string? Airline { get; set; }
        public string? FlightNumber { get; set; }
        public string AffiliateLink { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public bool IsRoundTrip { get; set; }
        public string? ReturnDate { get; set; }  // для round-trip
        public int Adults { get; set; } = 1;
        public int Transfers { get; set; }
    }
}
