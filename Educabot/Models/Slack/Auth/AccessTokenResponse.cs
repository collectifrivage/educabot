using Newtonsoft.Json;

namespace Educabot.Models.Slack.Auth
{
    public class AccessTokenResponse
    {
        [JsonProperty("error")]
        public string Error { get; set; }


        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
        [JsonProperty("scope")]
        public string Scope { get; set; }
        [JsonProperty("team_name")]
        public string TeamName { get; set; }
        [JsonProperty("team_id")]
        public string TeamId { get; set; }
    }
}