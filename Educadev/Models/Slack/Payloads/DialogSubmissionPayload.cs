using System.Collections.Generic;
using Newtonsoft.Json;

namespace Educadev.Models.Slack.Payloads
{
    public class DialogSubmissionPayload : Payload
    {
        [JsonProperty("submission")]
        public IDictionary<string, string> Submission { get; set; }
        [JsonProperty("state")]
        public string State { get; set; }
    }
}