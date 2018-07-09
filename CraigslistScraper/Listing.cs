using System;
using System.Collections.Generic;
using System.Text;

namespace CraigslistScraper
{
    public class Listing
    {
        public Listing()
        {
            this.ID = Guid.NewGuid().ToString();
        }
        public string ID { get; set; }
        public string ListingDate { get; set; }
        public string Link { get; set; }
        public string Title { get; set; }
        public string City { get; set; }
    }
}
