using System;
using Newtonsoft.Json;

namespace Educabot.Models.Slack
{
    public class SlackApiResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("response_metadata")]
        public ResponseMetadata Metadata { get; set; }

        public class ResponseMetadata
        {
            [JsonProperty("messages")]
            public string[] Messages { get; set; }
        }

        public void EnsureSuccess()
        {
            if (Ok) return;

            throw Metadata != null
                ? new Exception(Error + "\n\n" + string.Join("\n", Metadata?.Messages))
                : new Exception(Error);
        }
    }
}