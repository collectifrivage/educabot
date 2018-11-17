using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Educadev.Models.Slack;
using Educadev.Models.Slack.Auth;
using Educadev.Models.Slack.Dialogs;
using Educadev.Models.Slack.Messages;
using Educadev.Models.Slack.Payloads;
using Educadev.Models.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;

namespace Educadev.Helpers
{
    public static class SlackHelper
    {
        private static readonly HttpClient SlackHttpClient = new HttpClient {BaseAddress = new Uri("https://slack.com/api/")};
        private static HashAlgorithm GetSigningAlgorithm(string secret) => new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        
        public static async Task<string> ReadSlackRequest(HttpRequest req, ExecutionContext context)
        {
            var config = new ConfigHelper(context);

            var timestamp = req.Headers["X-Slack-Request-Timestamp"].Single();
            var body = await ReadAsString(req.Body);

            var valueToSign = $"v0:{timestamp}:{body}";
            var hashBytes = GetSigningAlgorithm(config.SigningSecret).ComputeHash(Encoding.UTF8.GetBytes(valueToSign));
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            var versionedHash = $"v0={hash}";

            var slackSignature = req.Headers["X-Slack-Signature"].Single();

            if (versionedHash != slackSignature)
            {
                throw new Exception($"Invalid signature. Computed {versionedHash}, received {slackSignature}.");
            }
            
            return body;
        }

        public static async Task<AccessTokenResponse> RequestAccessToken(string code, ExecutionContext context)
        {
            var config = new ConfigHelper(context);

            var request = new HttpRequestMessage(HttpMethod.Post, "oauth.access") {
                Content = new FormUrlEncodedContent(new Dictionary<string,string> {
                    {"code", code},
                    {"client_id", config.ClientId},
                    {"client_secret", config.ClientSecret}
                })
            };

            var response = await SlackHttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsAsync<AccessTokenResponse>();

            if (!string.IsNullOrWhiteSpace(result.Error))
                throw new Exception(result.Error);

            return result;
        }

        public static Task PostMessage(IBinder binder, string teamId, PostMessageRequest request) => AuthenticatedPost(binder, "chat.postMessage", teamId, request);
        public static Task PostEphemeral(IBinder binder, string teamId, PostEphemeralRequest request) => AuthenticatedPost(binder, "chat.postEphemeral", teamId, request);
        public static Task OpenDialog(IBinder binder, string teamId, OpenDialogRequest request) => AuthenticatedPost(binder, "dialog.open", teamId, request);
        public static Task UpdateMessage(IBinder binder, string teamId, UpdateMessageRequest request) => AuthenticatedPost(binder, "chat.update", teamId, request);

        private static async Task AuthenticatedPost(IBinder binder, string slackMethod, string teamId, object requestModel)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, slackMethod);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessToken(binder, teamId));
            request.Content = new StringContent(
                JsonConvert.SerializeObject(
                    requestModel,
                    new JsonSerializerSettings {
                        NullValueHandling = NullValueHandling.Ignore,
                        DefaultValueHandling = DefaultValueHandling.Ignore
                    }),
                Encoding.Default,
                "application/json");

            var response = await SlackHttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsAsync<SlackApiResponse>();
            result.EnsureSuccess();
        }

        public static IDictionary<string, string> ParseBody(string body)
        {
            return body.Split('&').Select(x => x.Split('=')).ToDictionary(x => x[0], x => HttpUtility.UrlDecode(x[1]));
        }
        
        private static async Task<string> GetAccessToken(IBinder binder, string teamId)
        {
            var team = await binder.GetTableRow<Team>("teams", "teams", teamId);
            if (team == null) throw new ArgumentException($"No access token for team {teamId}");

            return team.AccessToken;
        }

        private static async Task<string> ReadAsString(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public static Payload DecodePayload(string json)
        {
            var payload = JsonConvert.DeserializeObject<Payload>(json);

            if (payload.Type == "dialog_submission")
                return JsonConvert.DeserializeObject<DialogSubmissionPayload>(json);
            if (payload.Type == "interactive_message")
                return JsonConvert.DeserializeObject<InteractiveMessagePayload>(json);

            return payload;
        }
    }
}