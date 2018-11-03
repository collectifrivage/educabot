using System.Collections.Generic;
using Newtonsoft.Json;

namespace Educadev.Models.Slack.Messages
{
    public class PostMessageRequest
    {
        [JsonProperty("channel")]
        public string Channel { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("as_user")]
        public bool AsUser { get; set; }
        [JsonProperty("attachments")]
        public IList<MessageAttachment> Attachments { get; set; }
        [JsonProperty("icon_emoji")]
        public string IconEmoji { get; set; }
        [JsonProperty("icon_url")]
        public string IconUrl { get; set; }
        [JsonProperty("link_names")]
        public bool LinkNames { get; set; }
        [JsonProperty("mrkdwn")]
        public bool Markdown { get; set; } = true;
        [JsonProperty("parse")]
        public string Parse { get; set; }
        [JsonProperty("reply_broadcast")]
        public bool ReplyBroadcast { get; set; }
        [JsonProperty("thread_ts")]
        public string ThreadTimestamp { get; set; }
        [JsonProperty("unfurl_links")]
        public bool UnfurlLinks { get; set; }
        [JsonProperty("unfurl_media")]
        public bool UnfurlMedia { get; set; } = true;
        [JsonProperty("username")]
        public string Username { get; set; }
    }
}