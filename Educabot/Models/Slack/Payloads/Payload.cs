using Educabot.Helpers;
using Newtonsoft.Json;

namespace Educabot.Models.Slack.Payloads
{
    public class Payload
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("callback_id")]
        public string CallbackId { get; set; }
        [JsonProperty("action_ts")]
        public string ActionTimestamp { get; set; }
        [JsonProperty("response_url")]
        public string ResponseUrl { get; set; }

        [JsonProperty("team")]
        public SlackTeam Team { get; set; }
        [JsonProperty("user")]
        public SlackUser User { get; set; }
        [JsonProperty("channel")]
        public SlackChannel Channel { get; set; }

        public string PartitionKey => Utils.GetPartitionKey(Team.Id, Channel.Id);
    }
}