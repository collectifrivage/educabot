using System.Collections.Generic;
using Newtonsoft.Json;

namespace Educabot.Models.Slack.Dialogs
{
    public class Dialog
    {
        [JsonProperty("callback_id")]
        public string CallbackId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("submit_label")]
        public string SubmitLabel { get; set; }

        [JsonProperty("notify_on_cancel")]
        public bool NotifyOnCancel { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("elements")]
        public List<DialogElement> Elements { get; set; }
    }
}