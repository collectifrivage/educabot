using System;
using System.Buffers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;

namespace Educadev.Helpers
{
    public static class Utils
    {
        public static DateTime GetLocalNow()
        {
            // TODO: Rendre time zone configurable
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }

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
    }
}