using System;
using System.Collections.Generic;
using System.Text;

namespace WeatherForecastLoader.DataModel
{
    internal class Weather
    {
        internal int Tempreture_min { get; set; }
        internal int Tempreture_max { get; set; }
        internal int Pressure_min { get; set; }
        internal int Pressure_max { get; set; }
        internal int Humidity { get; set; }
        internal int Radiation { get; set; }
        internal int Geomagnetic { get; set; }
        internal string Cloudiness { get; set; }
        internal Wind Wind { get; set; }
        internal string Precipitation { get; set; }
    }
}
