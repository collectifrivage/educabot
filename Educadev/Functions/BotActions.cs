﻿using System;
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
        static BotActions()
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
        }

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
                    var result = await ValidatePlan(binder, dsp);
                    if (!result.Valid) return Utils.Ok(result);

                    return await RecordPlan(binder, dsp);
                }
            }
            else if (payload is InteractiveMessagePayload imp)
            {
                switch (imp.CallbackId)
                {
                    case "message_action" when imp.Actions.First().Name == "removeme":
                        return Utils.Ok(new SlackMessage {DeleteOriginal = true});
                    case "plan_action":
                        return await ProcessPlanAction(binder, imp);
                    case "proposal_action":
                        return await ProcessProposalAction(binder, imp);
                }
            }

            return Utils.Ok();
        }

        private static SlackErrorsResponse ValidateProposal(DialogSubmissionPayload dsp)
        {
            var response = new SlackErrorsResponse();

            if (!Regex.IsMatch(dsp.GetValue("url"), @"^(https?://|\\\\)"))
                response.AddError("url", @"L'URL doit commencer par http://, https:// ou \\");

            return response;
        }

        private static async Task<SlackErrorsResponse> ValidatePlan(IBinder binder, DialogSubmissionPayload dsp)
        {
            var response = new SlackErrorsResponse();
            var now = DateTime.Now;
            
            if (!DateTime.TryParseExact(dsp.GetValue("date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var date))
                response.AddError("date", "Le format de date n'est pas valide.");
            else if (date < now.Date)
                response.AddError("date", "La date ne peut pas être dans le passé.");
            else if (now.TimeOfDay > TimeSpan.Parse("12:00") && date == now.Date)
                response.AddError("date", "Comme il est passé midi, la date ne peut pas être aujourd'hui.");
            
            if (!response.Valid) return response;

            var plansTable = await binder.GetTable("plans");
            var query = new TableQuery<Plan>().Where(TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("PartitionKey", "eq", dsp.PartitionKey),
                "and",
                TableQuery.GenerateFilterConditionForDate("Date", "eq", date)
            ));
            var plans = await plansTable.ExecuteQueryAsync(query);
            if (plans.Any())
            {
                response.AddError("date", "Un Lunch & Watch est déjà prévu pour cette date.");
            }

            if (date == now.Date)
            {
                if (string.IsNullOrWhiteSpace(dsp.GetValue("owner")))
                    response.AddError("owner", "Comme la présentation est prévue aujourd'hui, un responsable doit être désigné.");
                if (string.IsNullOrWhiteSpace(dsp.GetValue("video")))
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

                ProposedBy = proposalPayload.User.Id,
                Team = proposalPayload.Team.Id,
                Channel = proposalPayload.Channel.Id,

                Name = proposalPayload.GetValue("name"),
                Url = proposalPayload.GetValue("url"),
                Notes = proposalPayload.GetValue("notes")
            };

            await proposals.AddAsync(proposal);

            await SlackHelper.SlackPost("chat.postMessage", proposalPayload.Team.Id, new PostMessageRequest {
                Text = $"<@{proposalPayload.User.Id}> vient de proposer un vidéo :",
                Channel = proposalPayload.Channel.Id,
                Attachments = new [] {
                    new MessageAttachment {
                        Title = proposal.GetFormattedTitle(),
                        Text = proposal.Notes,
                        Footer = "Utilisez /edu:list pour voir toutes les propositions"
                    }
                }
            });
        }

        private static async Task<IActionResult> RecordPlan(IBinder binder, DialogSubmissionPayload planPayload)
        {
            var plans = await binder.GetTable("plans");
            var proposals = await binder.GetTable("proposals");

            var videoKey = planPayload.GetValue("video");
            if (!string.IsNullOrWhiteSpace(videoKey))
            {
                var proposal = await proposals.Retrieve<Proposal>(planPayload.PartitionKey, videoKey);
                if (proposal == null)
                {
                    return Utils.Ok(new SlackMessage {
                        Text = "Vidéo non trouvé",
                        Attachments = {MessageHelpers.GetRemoveMessageAttachment()}
                    });
                }

                if (!string.IsNullOrWhiteSpace(proposal.PlannedIn))
                {
                    var otherPlan = await plans.Retrieve<Plan>(planPayload.PartitionKey, proposal.PlannedIn);
                    if (otherPlan != null)
                    {
                        return Utils.Ok(new SlackMessage {
                            Text = $"Ce vidéo est déjà planifié pour le {otherPlan.Date:dddd d MMMM}."
                        });
                    }
                }

                proposal.PlannedIn = planPayload.ActionTimestamp;

                var proposalResult = await proposals.ExecuteAsync(TableOperation.Replace(proposal));
                if (proposalResult.IsError())
                {
                    await MessageHelpers.PostErrorMessage(planPayload);
                    return Utils.Ok();
                }
            }

            var plan = new Plan {
                PartitionKey = planPayload.PartitionKey,
                RowKey = planPayload.ActionTimestamp,
                CreatedBy = planPayload.User.Id,
                Team = planPayload.Team.Id,
                Channel = planPayload.Channel.Id,
                Date = DateTime.ParseExact(planPayload.GetValue("date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal),
                Owner = planPayload.GetValue("owner"),
                Video = videoKey
            };

            var result = await plans.ExecuteAsync(TableOperation.Insert(plan));
            if (result.IsError())
            {
                await MessageHelpers.PostErrorMessage(planPayload);
                return Utils.Ok();
            }

            var message = new PostMessageRequest {
                Text = $"<@{planPayload.User.Id}> vient de planifier un Lunch & Watch :",
                Channel = planPayload.Channel.Id,
                Attachments = await MessageHelpers.GetPlanAttachments(binder, plan)
            };

            await SlackHelper.SlackPost("chat.postMessage", planPayload.Team.Id, message);
            return Utils.Ok();
        }

        private static async Task<IActionResult> ProcessPlanAction(IBinder binder, InteractiveMessagePayload payload)
        {
            var plans = await binder.GetTable("plans");

            var action = payload.Actions.First();
            var plan = await plans.Retrieve<Plan>(payload.PartitionKey, action.Value);

            if (action.Name == "volunteer")
            {
                if (plan.Owner != null)
                {
                    var message = new PostEphemeralRequest {
                        User = payload.User.Id,
                        Channel = payload.Channel.Id,
                        Text = $"<@{plan.Owner}> est déjà responsable de ce Lunch & Watch.",
                        Attachments = new List<MessageAttachment> {
                            MessageHelpers.GetRemoveMessageAttachment()
                        }
                    };
                    await SlackHelper.SlackPost("chat.postEphemeral", payload.Team.Id, message);
                    
                    return await UpdatePlanMessage(binder, payload, plan, "");
                }

                plan.Owner = payload.User.Id;
            }

            var result = await plans.ExecuteAsync(TableOperation.Replace(plan));
            if (result.IsError())
            {
                await MessageHelpers.PostErrorMessage(payload);
                return await UpdatePlanMessage(binder, payload, plan, "");
            }
            
            return await UpdatePlanMessage(binder, payload, plan, "Merci!");
        }

        private static async Task<IActionResult> UpdatePlanMessage(IBinder binder, InteractiveMessagePayload payload, Plan plan, string ephemeralText)
        {
            if (payload.OriginalMessage != null)
            {
                // Message publique

                var message = new UpdateMessageRequest {
                    Text = payload.OriginalMessage.Text,
                    Channel = payload.Channel.Id,
                    Timestamp = payload.MessageTimestamp,
                    Attachments = await MessageHelpers.GetPlanAttachments(binder, plan)
                };

                await SlackHelper.SlackPost("chat.update", payload.Team.Id, message);
                return Utils.Ok();
            }
            else
            {
                // Message ephémère

                var message = new SlackMessage {
                    Text = ephemeralText,
                    Attachments = await MessageHelpers.GetPlanAttachments(binder, plan),
                    ReplaceOriginal = true
                };

                message.Attachments.Add(MessageHelpers.GetRemoveMessageAttachment());
                return Utils.Ok(message);
            }
        }

        private static async Task<IActionResult> ProcessProposalAction(IBinder binder, InteractiveMessagePayload payload)
        {
            var proposals = await binder.GetTable("proposals");
            var action = payload.Actions.First();
            var proposal = await proposals.Retrieve<Proposal>(payload.PartitionKey, action.Value);

            if (action.Name == "delete")
            {
                // Bloquer la suppression d'une proposition plannifiée
                if (!string.IsNullOrWhiteSpace(proposal.PlannedIn))
                {
                    var plan = await binder.GetTableRow<Plan>("plans", payload.PartitionKey, proposal.PlannedIn);
                    if (plan != null)
                    {
                        return Utils.Ok(new SlackMessage {
                            Text = $"Ce vidéo est déjà planifié pour le {plan.Date:dddd d MMMM}.",
                            Attachments = {MessageHelpers.GetRemoveMessageAttachment()}
                        });
                    }
                }

                var result = await proposals.ExecuteAsync(TableOperation.Delete(proposal));
                if (result.IsError())
                {
                    await MessageHelpers.PostErrorMessage(payload);
                    return Utils.Ok();
                }
                
                // Notifier le owner si c'est pas lui qui supprime sa proposition
                if (proposal.ProposedBy != payload.User.Id)
                {
                    await SlackHelper.SlackPost("chat.postMessage", payload.Team.Id, new PostMessageRequest {
                        Channel = proposal.ProposedBy,
                        Text = $"<@{payload.User.Id}> vient de supprimer votre proposition de vidéo dans <#{payload.Channel.Id}>:",
                        Attachments = { await MessageHelpers.GetProposalAttachment(binder, proposal, allowActions: false) }
                    });
                }
                
                // NOTE: Présentement la seule place qu'on peut supprimer une proposition c'est à partir de la liste de toutes les propositions.
                // Si ça change, va falloir ajouter plus de logique ici pour recréer le bon type de message.
                var allProposals = await proposals.GetAllByPartition<Proposal>(proposal.PartitionKey);
                var message = await MessageHelpers.GetListMessage(binder, allProposals, payload.Channel.Id);
                message.ReplaceOriginal = true;
                return Utils.Ok(message);
            }

            return Utils.Ok();
        }
    }
}