using System;
using System.Buffers;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;

namespace Educadev.Helpers
{
    public static class Utils
    {
        public static IActionResult Ok() => new OkResult();

        public static IActionResult Ok(object obj)
        {
            return new OkObjectResult(obj) {
                Formatters = new FormatterCollection<IOutputFormatter> {
                    new JsonOutputFormatter(new JsonSerializerSettings {
                        NullValueHandling = NullValueHandling.Ignore
                    }, ArrayPool<char>.Shared)
                }
            };
        }

        public static string GetPartitionKey(string team, string channel) => $"{team}:{channel}";
        public static string GetPartitionKey(string team, string channel, string addon) => GetPartitionKeyWithAddon(GetPartitionKey(team, channel), addon);
        public static string GetPartitionKeyWithAddon(string partitionKey, string addon) => $"{partitionKey}:{addon}";
        public static string GetChannelFromPartitionKey(string partitionKey) => partitionKey.Split(':')[1];

        public static void SetCulture()
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
        }

        public static bool TryParseDate(string dateString, out DateTime result)
        {
            return DateTime.TryParseExact(
                dateString, 
                "yyyy-MM-dd", 
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, 
                out result);
        }

        public static DateTime ParseDate(string dateString)
        {
            return DateTime.ParseExact(
                dateString, 
                "yyyy-MM-dd", 
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal);
        }
    }
}