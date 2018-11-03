using Newtonsoft.Json;

namespace Educadev.Models.Slack.Payloads
{
    public class SlackUser
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}