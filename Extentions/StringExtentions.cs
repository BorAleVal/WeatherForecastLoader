using System;
using System.Collections.Generic;
using System.Text;

namespace WeatherForecastLoader.Extentions
{
    internal static class StringExtentions
    {
        internal static bool ContainsMatch(this string Str, string targetString)
        {
            return Str.Equals(targetString)
                || Str.Contains(" " + targetString)
                || Str.Contains(targetString + " ");
        }

        internal static bool ContainsMatch(this string Str, string targetString, StringComparison comparison)
        {
            return Str.Equals(targetString, comparison)
                || Str.Contains(" " + targetString, comparison)
                || Str.Contains(targetString + " ", comparison);
        }
    }
}
