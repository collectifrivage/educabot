using Newtonsoft.Json;

namespace Educabot.Models.Slack.Dialogs
{
    public class SelectDialogElement : DialogElement
    {
        public SelectDialogElement(string name, string label) : base("select", name, label)
        {
        }

        [JsonProperty("data_source")]
        public string DataSource { get; set; }

        [JsonProperty("min_query_length")]
        public int? MinQueryLength { get; set; }

        [JsonProperty("selected_options")]
        public SelectOption[] SelectedOptions { get; set; }

        [JsonProperty("options")]
        public SelectOption[] Options { get; set; }

        [JsonProperty("option_groups")]
        public SelectOptionGroup[] OptionGroups { get; set; }
    }
}