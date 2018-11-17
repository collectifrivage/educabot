using Newtonsoft.Json;

namespace Educabot.Models.Slack.Dialogs
{
    public abstract class DialogElement
    {
        protected DialogElement(string type, string name, string label)
        {
            Type = type;
            Name = name;
            Label = label;
        }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("hint")]
        public string Hint { get; set; }

        [JsonProperty("type")]
        public string Type { get; protected set; }

        [JsonProperty("value")]
        public string DefaultValue { get; set; }

        [JsonProperty("placeholder")]
        public string Placeholder { get; set; }

        [JsonProperty("optional")]
        public bool Optional { get;set; }
    }
}