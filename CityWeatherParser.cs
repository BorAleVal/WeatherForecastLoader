using HtmlAgilityPack;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WeatherForecastLoader
{
    internal class CityWeatherParser
    {
        public string CityName { get; }
        private HtmlWeb web = new HtmlWeb();
        private const string TEN_DAYS_URL_APPEND = "10-days/";
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public CityWeatherParser(string cityName, string url)
        {
            try
            {
                CityName = cityName;
                var doc = web.Load(url + TEN_DAYS_URL_APPEND);

                var popularCityNode = doc
                    .DocumentNode
                    .Descendants("div")
                    .FirstOrDefault(x => x.Attributes["class"]?.Value == "");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

    }
}
