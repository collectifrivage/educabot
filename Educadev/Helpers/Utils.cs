using System.Buffers;
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
        public static string GetChannelFromPartitionKey(string partitionKey) => partitionKey.Split(':')[1];
    }
}