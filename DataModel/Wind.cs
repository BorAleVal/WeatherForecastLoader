using System;
using System.Collections.Generic;
using System.Text;

namespace WeatherForecastLoader.DataModel
{
    internal class Wind
    {
        internal int MinSpeed { get; set; }
        internal int MaxSpeed { get; set; }
        internal string Direction { get; set; }
    }
}
