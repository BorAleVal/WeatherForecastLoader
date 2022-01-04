using HtmlAgilityPack;
using MongoDB.Bson;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WeatherForecastLoader.DataModel;
using WeatherForecastLoader.Extentions;

namespace WeatherForecastLoader
{
    enum ContentEnum { weather, wind, pressure, humidity, radiation, geomagnetic };
    public class CityWeather
    {
        public string CityName { get; set; }
        public DateTime Date { get; set; }
        public Weather Weather { get; set; } = new Weather();
        [MongoDB.Bson.Serialization.Attributes.BsonId]
        public ObjectId? id { get { return ObjectId.GenerateNewId(); } set { } }

        private const int DAYS_COUNT = 10;
        private const string DAYS_URL_APPEND = "-days/";
        private HtmlWeb web = new HtmlWeb();

        private static Logger logger = LogManager.GetCurrentClassLogger();
        public CityWeather(string cityName, string url)
        {
            try
            {
                CityName = cityName;
                var doc = web.Load(url + DAYS_COUNT.ToString() + DAYS_URL_APPEND);

                var weatherNodes = doc
                    .DocumentNode
                    .Descendants("div");

                foreach (HtmlNode node in weatherNodes)
                {
                    if (node.Attributes["data-widget"]?.Value != null && Enum.IsDefined(typeof(ContentEnum), node.Attributes["data-widget"]?.Value))
                    {
                        var weatherEnum = Enum.Parse(typeof(ContentEnum), node.Attributes["data-widget"].Value);
                        switch (weatherEnum)
                        {
                            case ContentEnum.weather:
                                CompleteWeather(node);
                                break;
                            case ContentEnum.wind:
                                CompleteWind(node);
                                break;
                            case ContentEnum.geomagnetic:
                                CompleteGeomagnetic(node);
                                break;
                            case ContentEnum.humidity:
                                CompleteHumidity(node);
                                break;
                            case ContentEnum.pressure:
                                CompletePressure(node);
                                break;
                            case ContentEnum.radiation:
                                CompleteRadiation(node);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Write(ex);
#else
                logger.Error(ex);
#endif
            }
        }

        private void CompleteGeomagnetic(HtmlNode node)
        {
            Weather.Geomagnetic = node
                .Descendants("div")
                .First(x => x.Attributes["class"].Value.ContainsMatch("widget-row-geomagnetic", StringComparison.InvariantCultureIgnoreCase))
                .Descendants("div")
                .Where(x => x.Attributes["class"].Value.ContainsMatch("item", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.InnerText.Replace("-", "0")))
                .ToArray();
        }

        private void CompleteHumidity(HtmlNode node)
        {
            Weather.Humidity = node
                .Descendants("div")
                .First(x => x.Attributes["class"].Value.ContainsMatch("widget-row-humidity", StringComparison.InvariantCultureIgnoreCase))
                .ChildNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("row-item", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.InnerText))
                .ToArray();
        }

        private void CompleteRadiation(HtmlNode node)
        {
            Weather.Radiation = node
                .Descendants("div")
                .First(x => x.Attributes["class"].Value.ContainsMatch("widget-row-radiation", StringComparison.InvariantCultureIgnoreCase))
                .Descendants("div")
                .Where(x => x.Attributes["class"].Value.ContainsMatch("row-item", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.InnerText.Replace("-", "0")))
                .ToArray();
        }

        private void CompleteWeather(HtmlNode node)
        {
            var divNodes = node.Descendants("div");

            // Contains используется потому что помимо основного класса может быть что угодно.
            var date = DateTime.Parse(divNodes
                .First(x => x.Attributes["class"].Value.Equals("date", StringComparison.InvariantCultureIgnoreCase))
                .InnerText);

            Date = DateTime.SpecifyKind(date, DateTimeKind.Utc);

            Weather.Cloudiness = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("weather-icon", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.Attributes["data-text"].Value)
                .ToArray();
            Weather.TempretureMax = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("maxt", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.FirstChild.InnerText.Replace(@"&minus;", "-")))
                .ToArray();
            Weather.TempretureMin = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("mint", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.FirstChild.InnerText.Replace(@"&minus;", "-")))
                .ToArray();

            Weather.Precipitation = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("item-unit", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => double.Parse(x.InnerText))
                .ToArray();
            // TODO : выяснить как быстрее найти строку и в ней искать объекты или сразу по всем divNodes
        }

        private void CompleteWind(HtmlNode node)
        {
            var divNodes = node.Descendants("div");

            Weather.Wind.AvgSpeed = divNodes
                .First(x => x.Attributes["class"].Value.ContainsMatch("widget-row-wind-speed", StringComparison.InvariantCultureIgnoreCase))
                .Descendants("span")
                .Where(x => x.Attributes["class"].Value.ContainsMatch("unit_wind_m_s", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.InnerText))
                .ToArray();

            // TODO : как-то нужно обрабатывать случай когда попадается <span>-</span>
            Weather.Wind.GustSpeed = divNodes
                .First(x => x.Attributes["class"] != null && x.Attributes["class"].Value.ContainsMatch("widget-row-wind-gust", StringComparison.InvariantCultureIgnoreCase))
                .Descendants("span")
                .Where(x => x.Attributes["class"] != null
                && x.Attributes["class"].Value.ContainsMatch("unit_wind_m_s", StringComparison.InvariantCultureIgnoreCase)
                && x.Attributes["class"].Value.ContainsMatch("wind-unit", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.InnerText))
                .ToArray();

            Weather.Wind.Direction = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("direction", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.InnerText)
                .ToArray();
        }

        private void CompletePressure(HtmlNode node)
        {
            var divNodes = node.Descendants("div");

            Weather.PressureMax = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("maxt", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.FirstChild.InnerText))
                .ToArray();

            Weather.PressureMin = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("mint", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.FirstChild.InnerText))
                .ToArray();
        }
    }
}
