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
using Educadev.Models.Slack.Payloads;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Educadev
{
    public static class SlackHelper
    {
        private static readonly HttpClient SlackHttpClient = new HttpClient {BaseAddress = new Uri("https://slack.com/api/")};
        private static readonly HashAlgorithm SigningAlgorithm = new HMACSHA256(Encoding.UTF8.GetBytes(GetSigningSecret()));
        
        public static async Task<string> ReadSlackRequest(HttpRequest req)
        {
            var timestamp = req.Headers["X-Slack-Request-Timestamp"].Single();
            var body = await ReadAsString(req.Body);

            var valueToSign = $"v0:{timestamp}:{body}";
            var hashBytes = SigningAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(valueToSign));
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            var versionedHash = $"v0={hash}";

            var slackSignature = req.Headers["X-Slack-Signature"].Single();

            if (versionedHash != slackSignature)
            {
                throw new Exception($"Invalid signature. Computed {versionedHash}, received {slackSignature}.");
            }
            
            return body;
        }

        public static async Task SlackPost(string slackMethod, string teamId, object requestModel)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, slackMethod);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetAccessToken(teamId));
            request.Content = new StringContent(
                JsonConvert.SerializeObject(
                    requestModel,
                    new JsonSerializerSettings {
                        NullValueHandling = NullValueHandling.Ignore
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

        private static string GetSigningSecret()
        {
            return "55752d29afb3e4adf696ea39963f8f04";
        }

        private static string GetAccessToken(string teamId)
        {
            return "xoxp-469507476454-467369585168-468129145906-a895995c60d13ac33e2cbc06c25ada3d";
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

        public static string GetPartitionKey(string team, string channel) => $"{team}:{channel}";
    }
}