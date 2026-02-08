using System;
using System.Collections.Generic;
using System.Text;

namespace ParserFlightTickets.Config
{
    public class SearchSettingsConfig
    {
        public int CheckIntervalMinutes { get; set; } = 60;

    //    public Dictionary<string, int> MaxPostsPerDay { get; set; } = new()
    //{
    //    { "Flights", 8 },
    //    { "Hotels", 5 },
    //    { "Tours", 4 }
    //};

    //    public Dictionary<string, int> MinPostsPerDay { get; set; } = new()
    //{
    //    { "Flights", 8 },
    //    { "Hotels", 5 },
    //    { "Tours", 4 }
    //};
        public int MinPrice {  get; set; }
        public int MaxPrice { get; set; }

        public List<string> RussianCities { get; set; } = new();
        public List<string> PopularDestinations { get; set; } = new();

        public List<string> PriorityRussianCities { get; set; } = new List<string> { "MOW", "LED", "SVX" };
        public List<string> PriorityPopularDestinations { get; set; } = new List<string> { "BKK", "DXB", "IST" };
    }
}
