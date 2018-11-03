using Newtonsoft.Json;

namespace Educadev.Models.Slack.Payloads
{
    public class SlackTeam
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("domain")]
        public string Domain { get; set; }
    }
}