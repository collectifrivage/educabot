using Newtonsoft.Json;

namespace Educabot.Models.Slack.Dialogs
{
    public class OpenDialogRequest
    {
        [JsonProperty("trigger_id")]
        public string TriggerId { get; set; }

        [JsonProperty("dialog")]
        public Dialog Dialog { get; set; }
    }
}