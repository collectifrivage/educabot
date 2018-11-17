using System.Collections.Generic;
using Newtonsoft.Json;

namespace Educabot.Models.Slack.Payloads
{
    public class DialogSubmissionPayload : Payload
    {
        public string GetValue(string field) => Submission.ContainsKey(field) ? Submission[field] : null;

        [JsonProperty("submission")]
        public IDictionary<string, string> Submission { get; set; }
        [JsonProperty("state")]
        public string State { get; set; }
    }
}