using System.Collections.Generic;
using Newtonsoft.Json;

namespace Educadev.Models.Slack.Messages
{
    public class MessageAttachment
    {
        [JsonProperty("attachment_type")]
        public string AttachmentType { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("fallback")]
        public string Fallback { get; set; } = "";

        [JsonProperty("pretext")]
        public string PreText { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("fields")]
        public List<AttachmentField> Fields { get; set; }

        [JsonProperty("author_name")]
        public string AuthorName { get; set; }
        [JsonProperty("author_icon")]
        public string AuthorIcon { get; set; }
        [JsonProperty("author_link")]
        public string AuthorLink { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("image_url")]
        public string ImageUrl { get; set; }

        [JsonProperty("callback_id")]
        public string CallbackId { get; set; }
        
        [JsonProperty("actions")]
        public List<MessageAction> Actions { get; set; }

        [JsonProperty("footer")]
        public string Footer { get; set; }
        [JsonProperty("footer_icon")]
        public string FooterIcon { get; set; }
        [JsonProperty("ts")]
        public string Timestamp { get; set; }

        [JsonProperty("mrkdwn_in")]
        public string[] MarkdownIn { get; set; }
    }
}