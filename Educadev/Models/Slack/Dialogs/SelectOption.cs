using Newtonsoft.Json;

namespace Educadev.Models.Slack.Dialogs
{
    public class SelectOption
    {
        [JsonProperty("label")]
        public string Label { get; set; }
        [JsonProperty("value")]
        public string Value { get; set; }
    }
}