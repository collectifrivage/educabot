using Newtonsoft.Json;

namespace Educadev.Models.Slack.Dialogs
{
    public class SelectOptionGroup
    {
        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("options")]
        public SelectOption[] Options { get; set; }
    }
}