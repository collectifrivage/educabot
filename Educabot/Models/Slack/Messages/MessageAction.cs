using Newtonsoft.Json;

namespace Educabot.Models.Slack.Messages
{
    public class MessageAction
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("value")]
        public string Value { get; set; }
        [JsonProperty("style")]
        public string Style { get; set; }
        [JsonProperty("confirm")]
        public ActionConfirmation Confirm { get; set; }
    }
}