using System.Collections.Generic;
using Newtonsoft.Json;

namespace Educadev.Models.Slack.Messages
{
    public class SlackMessage
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("attachments")]
        public IList<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();

        [JsonProperty("thread_ts")]
        public string ThreadTimestamp { get; set; }

        [JsonProperty("response_type")]
        public string ResponseType { get; set; }

        [JsonProperty("replace_original")]
        public bool ReplaceOriginal { get; set; }
        [JsonProperty("delete_original")]
        public bool DeleteOriginal { get; set; }
    }
}