using System;
using System.Collections.Generic;
using System.Text;

namespace ParserFlightTickets.Models
{
    public class TourDeal
    {
        public string Destination { get; set; } = string.Empty;
        public string HotelName { get; set; } = string.Empty;
        public int Stars { get; set; }
        public string MealType { get; set; } = string.Empty;
        public int DurationNights { get; set; }
        public decimal TotalPrice { get; set; }
        public string DepartureDate { get; set; } = string.Empty;
        public int Transfers { get; set; }
        public string AffiliateLink { get; set; } = string.Empty;
    }
}
