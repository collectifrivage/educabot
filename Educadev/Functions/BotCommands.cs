using System;
using System.Collections.Generic;
using System.Globalization;
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
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace Educadev.Functions
{
    public static class BotCommands
    {
        static BotCommands()
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
        }

        [FunctionName("SlackCommandPropose")]
        public static async Task<IActionResult> OnPropose(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/commands/propose")] HttpRequest req,
            ILogger log)
        {
            var body = await SlackHelper.ReadSlackRequest(req);
            var parameters = SlackHelper.ParseBody(body);

            var dialogRequest = new OpenDialogRequest {
                TriggerId = parameters["trigger_id"],
                Dialog = GetProposeDialog(defaultName: parameters["text"])
            };

            await SlackHelper.SlackPost("dialog.open", parameters["team_id"], dialogRequest);

            return Utils.Ok();
        }

        [FunctionName("SlackCommandList")]
        public static async Task<IActionResult> OnList(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/commands/list")] HttpRequest req,
            [Table("proposals")] CloudTable proposalsTable,
            ILogger log, IBinder binder)
        {
            var body = await SlackHelper.ReadSlackRequest(req);
            var parameters = SlackHelper.ParseBody(body);

            var team = parameters["team_id"];
            var channel = parameters["channel_id"];

            var allProposals = await proposalsTable.GetAllByPartition<Proposal>(SlackHelper.GetPartitionKey(team, channel));

            var message = await MessageHelpers.GetListMessage(binder, allProposals, channel);

            return Utils.Ok(message);
        }

        [FunctionName("SlackCommandPlan")]
        public static async Task<IActionResult> OnPlan(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/commands/plan")] HttpRequest req,
            [Table("proposals")] CloudTable proposalsTable,
            ILogger log)
        {
            var body = await SlackHelper.ReadSlackRequest(req);
            var parameters = SlackHelper.ParseBody(body);
            
            var allProposals = await proposalsTable.GetAllByPartition<Proposal>(SlackHelper.GetPartitionKey(parameters["team_id"], parameters["channel_id"]));

            var dialogRequest = new OpenDialogRequest {
                TriggerId = parameters["trigger_id"],
                Dialog = GetPlanDialog(allProposals, defaultDate: parameters["text"])
            };

            await SlackHelper.SlackPost("dialog.open", parameters["team_id"], dialogRequest);

            return Utils.Ok();
        }

        [FunctionName("SlackCommandNext")]
        public static async Task<IActionResult> OnNext(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/commands/next")] HttpRequest req,
            [Table("plans")] CloudTable plansTable,
            ILogger log, IBinder binder)
        {
            var body = await SlackHelper.ReadSlackRequest(req);
            var parameters = SlackHelper.ParseBody(body);
            var partitionKey = SlackHelper.GetPartitionKey(parameters["team_id"], parameters["channel_id"]);

            var futurePlansQuery = new TableQuery<Plan>()
                .Where(
                    TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition("PartitionKey", "eq", partitionKey),
                        "and",
                        TableQuery.GenerateFilterConditionForDate("Date", "gt", DateTime.Now))
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

        private static Dialog GetProposeDialog(string defaultName)
        {
            return new Dialog {
                CallbackId = "propose",
                Title = "Proposer un vidéo",
                SubmitLabel = "Proposer",
                Elements = new List<DialogElement> {
                    new TextDialogElement("name", "Nom du vidéo") {
                        MaxLength = 40,
                        Placeholder = "How to use a computer",
                        DefaultValue = defaultName
                    },
                    new TextDialogElement("url", "URL vers la vidéo") {
                        Subtype = "url",
                        Placeholder = "http://example.com/my-awesome-video",
                        Hint = @"Si le vidéo est sur le réseau, inscrivez le chemin vers le fichier partagé, débutant par \\"
                    },
                    new TextareaDialogElement("notes", "Notes") {
                        Optional = true
                    }
                }
            };
        }

        private static Dialog GetPlanDialog(IList<Proposal> allProposals, string defaultDate)
        {
            var dialog = new Dialog {
                CallbackId = "plan",
                Title = "Planifier un Lunch&Watch",
                SubmitLabel = "Planifier",
                Elements = new List<DialogElement> {
                    new TextDialogElement("date", "Date") {
                        Hint = "Au format AAAA-MM-JJ",
                        DefaultValue = defaultDate
                    },
                    new SelectDialogElement("owner", "Responsable") {
                        Optional = true,
                        DataSource = "users",
                        Hint = "Si non choisi, le bot va demander un volontaire."
                    }
                }
            };

            if (allProposals.Any())
            {
                dialog.Elements.Add(new SelectDialogElement("video", "Vidéo") {
                    Optional = true,
                    Options = allProposals
                        .Where(x => string.IsNullOrWhiteSpace(x.PlannedIn))
                        .Select(x => new SelectOption {
                            Label = x.Name,
                            Value = x.RowKey
                        }).ToArray(),
                    Hint = "Si non choisi, le bot va faire voter le channel."
                });
            }

            return dialog;
        }
    }
}
