using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Educadev.Helpers;
using Educadev.Models.Slack;
using Educadev.Models.Slack.Messages;
using Educadev.Models.Slack.Payloads;
using Educadev.Models.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace Educadev.Functions
{
    public static class BotActions
    {
        [FunctionName("SlackAction")]
        public static async Task<IActionResult> DispatchAction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/action-endpoint")] HttpRequest req, 
            IBinder binder,
            ILogger log)
        {
            var body = await SlackHelper.ReadSlackRequest(req);
            var parameters = SlackHelper.ParseBody(body);
            var payload = SlackHelper.DecodePayload(parameters["payload"]);

            if (payload is DialogSubmissionPayload dsp)
            {
                if (dsp.CallbackId == "propose")
                {
                    var result = ValidateProposal(dsp);
                    if (!result.Valid) return Utils.Ok(result);

                    await RecordProposal(binder, dsp);
                }
                else if (dsp.CallbackId == "plan")
                {
                    var result = ValidatePlan(dsp);
                    if (!result.Valid) return Utils.Ok(result);

                    await RecordPlan(binder, dsp);
                }
            }
            else if (payload is InteractiveMessagePayload imp)
            {
                switch (imp.CallbackId)
                {
                    case "message_action" when imp.Actions.First().Name == "removeme":
                        return Utils.Ok(new SlackMessage {DeleteOriginal = true});
                    case "plan_action":
                        await ProcessPlanAction(binder, imp);
                        break;
                    case "proposal_action":
                        return await ProcessProposalAction(binder, imp);
                }
            }

            return Utils.Ok();
        }

        private static SlackErrorsResponse ValidateProposal(DialogSubmissionPayload dsp)
        {
            var response = new SlackErrorsResponse();

            if (!Regex.IsMatch(dsp.Submission["url"], @"^(https?://|\\\\)"))
                response.AddError("url", @"L'URL doit commencer par http://, https:// ou \\");

            return response;
        }

        private static SlackErrorsResponse ValidatePlan(DialogSubmissionPayload dsp)
        {
            var response = new SlackErrorsResponse();
            var now = Utils.GetLocalNow();

            if (!DateTime.TryParseExact(dsp.Submission["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var date))
                response.AddError("date", "Le format de date n'est pas valide.");
            else if (date < now.Date)
                response.AddError("date", "La date ne peut pas être dans le passé.");
            else if (now.TimeOfDay > TimeSpan.Parse("12:00") && date == now.Date)
                response.AddError("date", "Comme il est passé midi, la date ne peut pas être aujourd'hui.");

            if (!response.Valid) return response;

            if (date == now.Date)
            {
                if (string.IsNullOrWhiteSpace(dsp.Submission["owner"]))
                    response.AddError("owner", "Comme la présentation est prévue aujourd'hui, un responsable doit être désigné.");
                if (string.IsNullOrWhiteSpace(dsp.Submission["video"]))
                    response.AddError("video", "Comme la présentation est prévue aujourd'hui, un vidéo doit être choisi.");
            }

            return response;
        }

        

        private static async Task RecordProposal(IBinder binder, DialogSubmissionPayload proposalPayload)
        {
            var proposals = await binder.GetTableCollector<Proposal>("proposals");

            var proposal = new Proposal {
                PartitionKey = proposalPayload.PartitionKey,
                RowKey = proposalPayload.ActionTimestamp,

                Name = proposalPayload.Submission["name"],
                Url = proposalPayload.Submission["url"],
                Notes = proposalPayload.Submission["notes"],
                ProposedBy = proposalPayload.User.Id
            };

            await proposals.AddAsync(proposal);

            await SlackHelper.SlackPost("chat.postMessage", proposalPayload.Team.Id, new PostMessageRequest {
                Text = $"<@{proposalPayload.User.Id}> vient de proposer un vidéo :",
                Channel = proposalPayload.Channel.Id,
                Attachments = new [] {
                    new MessageAttachment {
                        Title = proposal.GetFormattedTitle(),
                        Text = proposal.Notes,
                        Footer = "Utilisez /dev-list pour voir toutes les propositions"
                    }
                }
            });
        }

        private static async Task RecordPlan(IBinder binder, DialogSubmissionPayload planPayload)
        {
            var plans = await binder.GetTableCollector<Plan>("plans");

            var plan = new Plan {
                PartitionKey = planPayload.PartitionKey,
                RowKey = planPayload.ActionTimestamp,
                Date = DateTime.ParseExact(planPayload.Submission["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces),
                Owner = planPayload.Submission["owner"],
                Video = planPayload.Submission["video"]
            };

            await plans.AddAsync(plan);

            var message = new PostMessageRequest {
                Text = $"<@{planPayload.User.Id}> vient de planifier un Lunch & Watch :",
                Channel = planPayload.Channel.Id,
                Attachments = await MessageHelpers.GetPlanAttachments(binder, plan)
            };

            await SlackHelper.SlackPost("chat.postMessage", planPayload.Team.Id, message);
        }

        private static async Task ProcessPlanAction(IBinder binder, InteractiveMessagePayload payload)
        {
            var plans = await binder.GetTable("plans");

            var action = payload.Actions.First();
            var plan = await plans.Retrieve<Plan>(payload.PartitionKey, action.Value);

            if (action.Name == "volunteer")
            {
                if (plan.Owner != null)
                {
                    await SlackHelper.SlackPost("chat.postEphemeral", payload.Team.Id, new PostEphemeralMessageRequest {
                        Text = $"<@{plan.Owner}> est déjà responsable de ce Lunch & Watch.",
                        Attachments = new List<MessageAttachment> {
                            MessageHelpers.GetRemoveMessageAttachment()
                        }
                    });

                    return;
                }

                plan.Owner = payload.User.Id;
            }

            var result = await plans.ExecuteAsync(TableOperation.Replace(plan));
            if (result.HttpStatusCode >= 400)
            {
                await SlackHelper.SlackPost("chat.postEphemeral", payload.Team.Id, new PostEphemeralMessageRequest {
                    Text = "Oups! Il y a eu un problème. Ré-essayez ?",
                    Attachments = new List<MessageAttachment> {
                        MessageHelpers.GetRemoveMessageAttachment()
                    }
                });
            }

            var message = new UpdateMessageRequest {
                Text = payload.OriginalMessage.Text,
                Channel = payload.Channel.Id,
                Timestamp = payload.MessageTimestamp,
                Attachments = await MessageHelpers.GetPlanAttachments(binder, plan)
            };

            await SlackHelper.SlackPost("chat.update", payload.Team.Id, message);
        }

        private static async Task<IActionResult> ProcessProposalAction(IBinder binder, InteractiveMessagePayload payload)
        {
            var proposals = await binder.GetTable("proposals");
            var action = payload.Actions.First();
            var plan = await proposals.Retrieve<Proposal>(payload.PartitionKey, action.Value);

            if (action.Name == "delete")
            {
                // TODO: Ajouter de la logique pour ne pas supprimer une proposition assignée à un plan
                await proposals.ExecuteAsync(TableOperation.Delete(plan));
                
                // TODO: Notifier le owner si c'est pas lui qui supprime sa proposition
                
                // NOTE: Présentement la seule place qu'on peut supprimer une proposition c'est à partir de la liste de toutes les propositions.
                // Si ça change, va falloir ajouter plus de logique ici pour recréer le bon type de message.
                var allProposals = await proposals.GetAllByPartition<Proposal>(plan.PartitionKey);
                var message = MessageHelpers.GetListMessage(allProposals, payload.Channel.Id);
                message.ReplaceOriginal = true;
                return Utils.Ok(message);
            }

            return Utils.Ok();
        }
    }
}