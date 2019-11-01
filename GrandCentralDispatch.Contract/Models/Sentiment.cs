using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrandCentralDispatch.Contract.Models
{
    public class Sentiment
    {
        public DateTimeOffset Date { get; set; }

        public string SentimentText;

        public bool Value;

        public bool Prediction { get; set; }

        public float Probability { get; set; }

        public float Score { get; set; }
    }
}
