using System.Threading.Tasks;
using Educadev.Helpers;
using Educadev.Models.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.WindowsAzure.Storage.Table;

namespace Educadev.Functions
{
    public static class BotSetup
    {
        [FunctionName("InstallCallback")]
        public static async Task<IActionResult> InstallCallback(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "install")] HttpRequest req,
            [Table("teams")] CloudTable teamsTable,
            ExecutionContext context)
        {
            if (!req.GetQueryParameterDictionary().TryGetValue("code", out var code))
            {
                return new ContentResult {
                    Content = "Something went wrong: no code",
                    StatusCode = 400
                };
            }

            var accessTokenResponse = await SlackHelper.RequestAccessToken(code, context);
            var team = new Team(accessTokenResponse.TeamId, accessTokenResponse.AccessToken);
            await teamsTable.ExecuteAsync(TableOperation.InsertOrReplace(team));

            return new ContentResult {
                Content = "All set!"
            };
        }
    }
}