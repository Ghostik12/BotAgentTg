using System;
using System.Collections.Generic;
using System.Text;

namespace ParserFlightTickets.Models
{
    public class HotelDeal
    {
        public string CityCode { get; set; } = string.Empty;
        public string HotelName { get; set; } = string.Empty;
        public int Stars { get; set; }
        public decimal Rating { get; set; }
        public int PricePerNight { get; set; }
        public string MealType { get; set; } = string.Empty;
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public string AffiliateLink { get; set; } = string.Empty;
    }
}
