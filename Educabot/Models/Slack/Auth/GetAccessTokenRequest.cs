using Newtonsoft.Json;

namespace Educabot.Models.Slack.Auth
{
    public class GetAccessTokenRequest
    {
        [JsonProperty("client_id")]
        public string ClientId { get; set; }
        [JsonProperty("client_secret")]
        public string ClientSecret { get; set; }
        [JsonProperty("code")]
        public string Code { get; set; }
        [JsonProperty("redirect_uri")]
        public string RedirectUri { get; set; }
        [JsonProperty("single_channel")]
        public bool SingleChannel { get; set; }
    }
}