using System.Collections.Generic;
using Newtonsoft.Json;

namespace Educadev.Models.Slack
{
    public class SlackErrorsResponse
    {
        [JsonIgnore]
        public bool Valid => Errors.Count == 0;

        [JsonProperty("errors")]
        public List<SlackError> Errors { get; } = new List<SlackError>();

        public void AddError(string name, string error)
        {
            Errors.Add(new SlackError { Name = name, Error = error });
        }

        public class SlackError
        {
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("error")]
            public string Error { get; set; }
        }
    }
}