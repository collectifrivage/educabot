using System;
using System.Linq;
using System.Threading.Tasks;
using Educadev.Helpers;
using Educadev.Models.Slack.Dialogs;
using Educadev.Models.Slack.Messages;
using Educadev.Models.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.WindowsAzure.Storage.Table;

namespace Educadev.Functions
{
    public static class BotCommands
    {
        [FunctionName("SlackCommandPropose")]
        public static async Task<IActionResult> OnPropose(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/commands/propose")] HttpRequest req,
            IBinder binder, ExecutionContext context)
        {
            Utils.SetCulture();

            var body = await SlackHelper.ReadSlackRequest(req, context);
            var parameters = SlackHelper.ParseBody(body);

            var dialogRequest = new OpenDialogRequest {
                TriggerId = parameters["trigger_id"],
                Dialog = DialogHelpers.GetProposeDialog(defaultName: parameters["text"])
            };

            await SlackHelper.OpenDialog(binder, parameters["team_id"], dialogRequest);

            return Utils.Ok();
        }

        [FunctionName("SlackCommandList")]
        public static async Task<IActionResult> OnList(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/commands/list")] HttpRequest req,
            [Table("proposals")] CloudTable proposalsTable,
            IBinder binder, ExecutionContext context)
        {
            Utils.SetCulture();

            var body = await SlackHelper.ReadSlackRequest(req, context);
            var parameters = SlackHelper.ParseBody(body);

            var team = parameters["team_id"];
            var channel = parameters["channel_id"];
            
            var allProposals = await ProposalHelpers.GetActiveProposals(proposalsTable, Utils.GetPartitionKey(team, channel));
            var message = await MessageHelpers.GetListMessage(binder, allProposals, channel);

            return Utils.Ok(message);
        }

        [FunctionName("SlackCommandPlan")]
        public static async Task<IActionResult> OnPlan(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/commands/plan")] HttpRequest req,
            [Table("proposals")] CloudTable proposalsTable,
            IBinder binder, ExecutionContext context)
        {
            Utils.SetCulture();

            var body = await SlackHelper.ReadSlackRequest(req, context);
            var parameters = SlackHelper.ParseBody(body);
            var partitionKey = Utils.GetPartitionKey(parameters["team_id"], parameters["channel_id"]);

            var dialogRequest = new OpenDialogRequest {
                TriggerId = parameters["trigger_id"],
                Dialog = await DialogHelpers.GetPlanDialog(binder, partitionKey, defaultDate: parameters["text"])
            };

            await SlackHelper.OpenDialog(binder, parameters["team_id"], dialogRequest);

            return Utils.Ok();
        }

        [FunctionName("SlackCommandNext")]
        public static async Task<IActionResult> OnNext(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/commands/next")] HttpRequest req,
            [Table("plans")] CloudTable plansTable,
            IBinder binder, ExecutionContext context)
        {
            Utils.SetCulture();

            var body = await SlackHelper.ReadSlackRequest(req, context);
            var parameters = SlackHelper.ParseBody(body);
            var partitionKey = Utils.GetPartitionKey(parameters["team_id"], parameters["channel_id"]);

            var futurePlansQuery = new TableQuery<Plan>()
                .Where(
                    TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition("PartitionKey", "eq", partitionKey),
                        "and",
                        TableQuery.GenerateFilterConditionForDate("Date", "ge", DateTime.Now))
                );
            var futurePlans = await plansTable.ExecuteQueryAsync(futurePlansQuery);

            var attachmentTasks = futurePlans.OrderBy(x => x.Date).Select(x => MessageHelpers.GetPlanAttachment(binder, x));
            var message = new SlackMessage {
                Text = futurePlans.Any() 
                    ? "Voici les Lunch & Watch planifiés :"
                    : "Aucun Lunch & Watch n'est à l'horaire. Utilisez `/edu:plan` pour en planifier un!",
                Attachments = (await Task.WhenAll(attachmentTasks)).ToList()
            };

            message.Attachments.Add(MessageHelpers.GetRemoveMessageAttachment());

            return Utils.Ok(message);
        }
    }
}
