using Newtonsoft.Json;

namespace Educabot.Models.Slack.Payloads
{
    public class SlackTeam
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("domain")]
        public string Domain { get; set; }
    }
}