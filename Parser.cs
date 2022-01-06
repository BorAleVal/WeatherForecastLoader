using HtmlAgilityPack;
using MongoDB.Driver;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WeatherForecastLoader.Extentions;
using WeatherForecastModels;

namespace WeatherForecastLoader
{
    internal class Parser
    {
        private static string url = "https://www.gismeteo.ru/";
        private static HtmlWeb web = new HtmlWeb();
        private const string POPULAR_CITIES_NODE_CLASS = "cities-popular";
        private const int DAYS_COUNT = 10;
        private const string DAYS_URL_APPEND = "-days/";

        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static IMongoClient client = new MongoClient("mongodb://localhost:27017");
        private static IMongoDatabase database = client.GetDatabase("WeatherForecastDB");
        private static IMongoCollection<CityWeather> collection;

        internal static void ParseWeatherForecast()
        {
            try
            {
                collection = database.GetCollection<CityWeather>(nameof(CityWeather));

                var doc = web.Load(url);

                var popularCityNode = doc
                    .DocumentNode
                    .Descendants("div")
                    .FirstOrDefault(x => x.Attributes["class"]?.Value == POPULAR_CITIES_NODE_CLASS);

                if (popularCityNode == null)
                {
                    logger.Error($"Получение данных невозможно. Не удалось найти узел с class = {POPULAR_CITIES_NODE_CLASS}");
                    return;
                }

                var cityListNode = popularCityNode
                    .ChildNodes
                    .FirstOrDefault(x => x.Attributes["class"]?.Value == "list");

                if (cityListNode == null)
                {
                    logger.Error($"Получение данных невозможно. Не удалось найти узел class = list в узле с class = {POPULAR_CITIES_NODE_CLASS}");
                    return;
                }

                var cityNodeColl = cityListNode
                    .ChildNodes
                    .Select(x => x.ChildNodes.FirstOrDefault(y => y.Attributes["class"]?.Value == "link" && y.Attributes["href"] != null))
                    .Where(x => x != null)
                    .ToArray();

                if (cityNodeColl.Count() == 0)
                {
                    logger.Error($"Не удалось получить список городов.");
                    return;
                }

                var CityWeathers = cityNodeColl.Select(x => ParseWeatherForecast(x.InnerText, url + x.Attributes["href"].Value));

                var cityUpdateList = new List<WriteModel<CityWeather>>();
                var cityInsertList = new List<CityWeather>();

                // Можно было бы удалить все перед вставкой, но это будет два раза дольше. Возможно еще нужно удалять из БД неактуальные.
                foreach (CityWeather cityWeather in CityWeathers)
                {
                    var filterDefinition = Builders<CityWeather>.Filter.Eq(x => x.CityName, cityWeather.CityName);
                    if (collection.Find(filterDefinition).Any())
                    {
                        var updateDefinition = Builders<CityWeather>.Update
                            .Set(x => x.Date, cityWeather.Date)
                            .Set(x => x.WeatherForecast, cityWeather.WeatherForecast);
                        cityUpdateList.Add(new UpdateManyModel<CityWeather>(filterDefinition, updateDefinition));
                    }
                    else
                    {
                        cityInsertList.Add(cityWeather);
                    }
                }

                if (cityUpdateList.Count > 0)
                {
                    collection.BulkWriteAsync(cityUpdateList);
                }

                if (cityInsertList.Count > 0)
                {
                    collection.InsertManyAsync(cityInsertList);
                }

                logger.Info("Данные получены.");
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

        private static CityWeather ParseWeatherForecast(string cityName, string url)
        {
            var result = new CityWeather();
            result.CityName = cityName;
            result.WeatherForecast = new Weather[DAYS_COUNT];
            for (int i = 0; i < DAYS_COUNT; i++)
            {
                result.WeatherForecast[i] = new Weather();
            }

            var doc = web.Load(url + DAYS_COUNT.ToString() + DAYS_URL_APPEND);

            var weatherNodes = doc
                .DocumentNode
                .Descendants("div");

            foreach (HtmlNode node in weatherNodes)
            {
                if (node.Attributes["data-widget"]?.Value != null && Enum.IsDefined(typeof(WeatherContentEnum), node.Attributes["data-widget"]?.Value))
                {
                    var weatherEnum = Enum.Parse(typeof(WeatherContentEnum), node.Attributes["data-widget"].Value);
                    switch (weatherEnum)
                    {
                        case WeatherContentEnum.weather:
                            CompleteWeather(node, ref result);
                            break;
                        case WeatherContentEnum.wind:
                            CompleteWind(node, ref result);
                            break;
                        case WeatherContentEnum.geomagnetic:
                            CompleteGeomagnetic(node, ref result);
                            break;
                        case WeatherContentEnum.humidity:
                            CompleteHumidity(node, ref result);
                            break;
                        case WeatherContentEnum.pressure:
                            CompletePressure(node, ref result);
                            break;
                        case WeatherContentEnum.radiation:
                            CompleteRadiation(node, ref result);
                            break;
                        default:
                            break;
                    }
                }
            }

            return result;
        }

        private static void CompleteGeomagnetic(HtmlNode node, ref CityWeather weather)
        {
            var geomagnetic = node
            .Descendants("div")
            .First(x => x.Attributes["class"].Value.ContainsMatch("widget-row-geomagnetic", StringComparison.InvariantCultureIgnoreCase))
            .Descendants("div")
            .Where(x => x.Attributes["class"].Value.ContainsMatch("item", StringComparison.InvariantCultureIgnoreCase))
            .Select(x => int.Parse(x.InnerText))
            .ToArray();

            if (!CheckData(weather, geomagnetic, nameof(geomagnetic)))
            {
                return;
            }


            for (int i = 0; i < DAYS_COUNT; i++)
            {
                weather.WeatherForecast[i].Geomagnetic = geomagnetic[i];
            }
        }

        private static void CompleteHumidity(HtmlNode node, ref CityWeather weather)
        {
            var humidity = node
            .Descendants("div")
            .First(x => x.Attributes["class"].Value.ContainsMatch("widget-row-humidity", StringComparison.InvariantCultureIgnoreCase))
            .ChildNodes
            .Where(x => x.Attributes["class"].Value.ContainsMatch("row-item", StringComparison.InvariantCultureIgnoreCase))
            .Select(x => int.Parse(x.InnerText))
            .ToArray();

            if (!CheckData(weather, humidity, nameof(humidity)))
            {
                return;
            }

            for (int i = 0; i < DAYS_COUNT; i++)
            {
                weather.WeatherForecast[i].Humidity = humidity[i];
            }
        }

        private static void CompleteRadiation(HtmlNode node, ref CityWeather weather)
        {
            var radiation = node
            .Descendants("div")
            .First(x => x.Attributes["class"].Value.ContainsMatch("widget-row-radiation", StringComparison.InvariantCultureIgnoreCase))
            .Descendants("div")
            .Where(x => x.Attributes["class"].Value.ContainsMatch("row-item", StringComparison.InvariantCultureIgnoreCase))
            .Select(x => int.Parse(x.InnerText.Replace("-", "0")))
            .ToArray();

            if (!CheckData(weather, radiation, nameof(radiation)))
            {
                return;
            }

            for (int i = 0; i < DAYS_COUNT; i++)
            {
                weather.WeatherForecast[i].Radiation = radiation[i];
            }
        }

        private static void CompleteWeather(HtmlNode node, ref CityWeather weather)
        {
            var divNodes = node.Descendants("div");

            // Contains используется потому что помимо основного класса может быть что угодно.
            var date = DateTime.Parse(divNodes
                .First(x => x.Attributes["class"].Value.Equals("date", StringComparison.InvariantCultureIgnoreCase))
                .InnerText);

            weather.Date = DateTime.SpecifyKind(date, DateTimeKind.Utc);

            var cloudiness = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("weather-icon", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.Attributes["data-text"].Value)
                .ToArray();
            var tempretureMax = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("maxt", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.FirstChild.InnerText.Replace(@"&minus;", "-")))
                .ToArray();
            var tempretureMin = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("mint", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.FirstChild.InnerText.Replace(@"&minus;", "-")))
                .ToArray();
            var precipitation = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("item-unit", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => double.Parse(x.InnerText))
                .ToArray();

            if (!CheckData(weather, cloudiness, nameof(cloudiness))
                || !CheckData(weather, tempretureMax, nameof(tempretureMax))
                || !CheckData(weather, tempretureMin, nameof(tempretureMin))
                || !CheckData(weather, precipitation, nameof(precipitation)))
            {
                return;
            }

            for (int i = 0; i < DAYS_COUNT; i++)
            {
                weather.WeatherForecast[i].Cloudiness = cloudiness[i];
                weather.WeatherForecast[i].TempretureMax = tempretureMax[i];
                weather.WeatherForecast[i].TempretureMin = tempretureMin[i];
                weather.WeatherForecast[i].Precipitation = precipitation[i];
            }
        }

        private static void CompleteWind(HtmlNode node, ref CityWeather weather)
        {

            var divNodes = node.Descendants("div");

            var avgSpeed = divNodes
                .First(x => x.Attributes["class"].Value.ContainsMatch("widget-row-wind-speed", StringComparison.InvariantCultureIgnoreCase))
                .Descendants("span")
                .Where(x => x.Attributes["class"].Value.ContainsMatch("unit_wind_m_s", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.InnerText))
                .ToArray();

            var gustSpeed = divNodes
                .First(x => x.Attributes["class"] != null && x.Attributes["class"].Value.ContainsMatch("widget-row-wind-gust", StringComparison.InvariantCultureIgnoreCase))
                .Descendants("span")
                .Where(x => x.Attributes["class"] != null
                && x.Attributes["class"].Value.ContainsMatch("unit_wind_m_s", StringComparison.InvariantCultureIgnoreCase)
                && x.Attributes["class"].Value.ContainsMatch("wind-unit", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => int.Parse(x.InnerText))
                .ToArray();

            var direction = divNodes
                .Where(x => x.Attributes["class"].Value.ContainsMatch("direction", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.InnerText)
                .ToArray();

            if (!CheckData(weather, avgSpeed, nameof(avgSpeed))
                || !CheckData(weather, gustSpeed, nameof(gustSpeed))
                || !CheckData(weather, direction, nameof(direction)))
            {
                return;
            }

            for (int i = 0; i < DAYS_COUNT; i++)
            {
                weather.WeatherForecast[i].Wind.AvgSpeed = avgSpeed[i];
                weather.WeatherForecast[i].Wind.GustSpeed = gustSpeed[i];
                weather.WeatherForecast[i].Wind.Direction = direction[i];
            }
        }

        private static void CompletePressure(HtmlNode node, ref CityWeather weather)
        {
            var valNodes = node
                .Descendants("div")
                .Where(x => x.Attributes["class"].Value.ContainsMatch("value", StringComparison.InvariantCultureIgnoreCase));

            var pressure = valNodes
                .Select(x => new
                {
                    Maxt = x
                        .ChildNodes
                        .FirstOrDefault(x => x.Attributes["class"].Value.ContainsMatch("maxt", StringComparison.InvariantCultureIgnoreCase))?
                        .FirstChild
                        .InnerText,
                    Mint = x
                        .ChildNodes
                        .FirstOrDefault(x => x.Attributes["class"].Value.ContainsMatch("mint", StringComparison.InvariantCultureIgnoreCase))?
                        .FirstChild
                        .InnerText,
                }).ToArray();

            if (!CheckData(weather, pressure, nameof(pressure)))
            {
                return;
            }

            for (int i = 0; i < DAYS_COUNT; i++)
            {
                weather.WeatherForecast[i].PressureMax = int.Parse(pressure[i].Maxt);
                weather.WeatherForecast[i].PressureMin = string.IsNullOrEmpty(pressure[i].Mint) ? int.Parse(pressure[i].Maxt) : int.Parse(pressure[i].Mint);
            }
        }

        private static bool CheckData<T>(CityWeather weather, T[] data, string dataName)
        {
            if (data.Count() != DAYS_COUNT)
            {
                logger.Error($"Не удалось считать все данные о {dataName}. В городе {weather.CityName}");
                return false;
            }
            return true;
        }
    }

    internal enum WeatherContentEnum
    {
        weather,
        wind,
        pressure,
        humidity,
        radiation,
        geomagnetic
    }
}
