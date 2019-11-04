using System;

namespace GrandCentralDispatch.Contract.Models.ElasticSearch
{
    public class Review
    {
        public DateTimeOffset Date { get; set; }
        public string Title { get; set; }
        public string Overview { get; set; }
        public bool Liked { get; set; }
    }
}
