using Newtonsoft.Json;

namespace Educabot.Models.Slack.Messages
{
    public class ActionConfirmation
    {
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("ok_text")]
        public string OkText { get; set; }
        [JsonProperty("dismiss_text")]
        public string DismissText { get; set; }
    }
}