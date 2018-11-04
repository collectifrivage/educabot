using System.Collections.Generic;
using Newtonsoft.Json;

namespace Educadev.Models.Slack.Messages
{
    public class UpdateMessageRequest
    {
        [JsonProperty("channel")]
        public string Channel { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("ts")]
        public string Timestamp { get;set; }
        [JsonProperty("as_user")]
        public bool AsUser { get; set; }
        [JsonProperty("attachments")]
        public IList<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
        [JsonProperty("link_names")]
        public bool LinkNames { get; set; }
        [JsonProperty("parse")]
        public string Parse { get; set; }
    }
}