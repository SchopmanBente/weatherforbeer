using System;
using System.Collections.Generic;
using System.Text;

namespace WeatherForBeer
{
    public class BlobTriggerMessage : TriggerMessage
    {
        public WeatherRootObject Weather { get; set; }

        public BlobTriggerMessage(WeatherRootObject weather)
        {
            this.Weather = weather;
        }
    }
}
