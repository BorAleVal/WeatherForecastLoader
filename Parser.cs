using HtmlAgilityPack;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WeatherForecastLoader
{
    internal class Parser
    {

        // From Web
        private string url = "https://www.gismeteo.ru/";
        private HtmlWeb web = new HtmlWeb();
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private const string POPULAR_CITIES_NODE_CLASS = "cities-popular";
        private const string LIST_NODE_CLASS = "list";
        private const string LINK_NODE_CLASS = "link";

        public Parser()
        {
            try
            {
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
                    .FirstOrDefault(x => x.Attributes["class"]?.Value == LIST_NODE_CLASS);

                if (cityListNode == null)
                {
                    logger.Error($"Получение данных невозможно. Не удалось найти узел class = {LIST_NODE_CLASS} в узле с class = {POPULAR_CITIES_NODE_CLASS}");
                    return;
                }

                var cityList = cityListNode
                    .ChildNodes
                    .Select(x => x.ChildNodes.FirstOrDefault(y => y.Attributes["class"]?.Value == LINK_NODE_CLASS && y.Attributes["href"] != null))
                    .Where(x => x != null)
                    .ToList();
                var a = cityList.Select(x => new CityWeatherParser(x.InnerText, url + x.Attributes["href"].Value));
                //foreach (HtmlNode node in cityList)
                //{
                //    new CityWeather(node.InnerText, url + node.Attributes["href"].Value);
                //}
                var b = a.FirstOrDefault().CityName;
                logger.Info("Данные получены.");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }
    }
}
