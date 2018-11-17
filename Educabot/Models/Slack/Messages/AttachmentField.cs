using Newtonsoft.Json;

namespace Educabot.Models.Slack.Messages
{
    public class AttachmentField
    {
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("value")]
        public string Value { get; set; }
        [JsonProperty("short")]
        public bool Short { get; set; }
    }
}