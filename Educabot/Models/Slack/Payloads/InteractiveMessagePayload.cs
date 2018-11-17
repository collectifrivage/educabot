using Educabot.Models.Slack.Messages;
using Newtonsoft.Json;

namespace Educabot.Models.Slack.Payloads
{
    public class InteractiveMessagePayload : Payload
    {
        [JsonProperty("actions")]
        public Action[] Actions { get; set; }
        [JsonProperty("message_ts")]
        public string MessageTimestamp { get; set; }
        [JsonProperty("attachment_id")]
        public string AttachmentId { get; set; }

        [JsonProperty("original_message")]
        public SlackMessage OriginalMessage { get; set; }

        [JsonProperty("trigger_id")]
        public string TriggerId { get; set; }

        public class Action
        {
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("type")]
            public string Type { get; set; }
            [JsonProperty("value")]
            public string Value { get; set; }
        }
    }
}