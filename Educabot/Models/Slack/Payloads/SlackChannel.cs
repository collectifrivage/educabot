using Newtonsoft.Json;

namespace Educabot.Models.Slack.Payloads
{
    public class SlackChannel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}