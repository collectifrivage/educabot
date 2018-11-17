using Newtonsoft.Json;

namespace Educabot.Models.Slack.Dialogs
{
    public class TextDialogElement : DialogElement
    {
        public TextDialogElement(string name, string label) : base("text", name, label)
        {
        }

        [JsonProperty("max_length")]
        public int? MaxLength { get; set; }

        [JsonProperty("min_length")]
        public int? MinLength { get; set; }

        [JsonProperty("subtype")]
        public string Subtype { get; set; }
    }
}