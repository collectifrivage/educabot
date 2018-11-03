using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Educadev.Models.Slack;
using Educadev.Models.Slack.Dialogs;
using Educadev.Models.Slack.Messages;
using Educadev.Models.Slack.Payloads;
using Educadev.Models.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Educadev
{
    public static class EducadevBot
    {
        [FunctionName("SlackAction")]
        public static async Task<IActionResult> OnSlackAction(
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
                    if (!result.Valid) return Ok(result);

                    await binder.SendToQueue("proposals", dsp);
                }
                else if (dsp.CallbackId == "plan")
                {
                    var result = ValidatePlan(dsp);
                    if (!result.Valid) return Ok(result);

                    await binder.SendToQueue("plans", dsp);
                }
            }
            else if (payload is InteractiveMessagePayload imp)
            {
                switch (imp.CallbackId)
                {
                    case "message_action" when imp.Actions.First().Name == "removeme":
                        return Ok(new SlackMessage {DeleteOriginal = true});
                    case "plan_action":
                        await binder.SendToQueue("plan-actions", imp);
                        break;
                    case "proposal_action":
                        return await ProcessProposalAction(binder, imp);
                }
            }

            return Ok();
        }

        [FunctionName("SlackCommandPropose")]
        public static async Task<IActionResult> OnSlackCommandPropose(
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

            return Ok();
        }

        [FunctionName("SlackCommandList")]
        public static async Task<IActionResult> OnSlackCommandList(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/commands/list")] HttpRequest req,
            [Table("proposals")] CloudTable proposalsTable,
            ILogger log)
        {
            var body = await SlackHelper.ReadSlackRequest(req);
            var parameters = SlackHelper.ParseBody(body);

            var team = parameters["team_id"];
            var channel = parameters["channel_id"];

            var allProposals = await proposalsTable.GetAllByPartition<Proposal>(SlackHelper.GetPartitionKey(team, channel));

            var message = GetListMessage(allProposals, channel);

            return Ok(message);
        }

        [FunctionName("SlackCommandPlan")]
        public static async Task<IActionResult> OnSlackCommandPlan(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack/commands/plan")] HttpRequest req,
            [Table("proposals")] CloudTable proposalsTable,
            ILogger log)
        {
            var body = await SlackHelper.ReadSlackRequest(req);
            var parameters = SlackHelper.ParseBody(body);
            
            var allProposals = await proposalsTable.GetAllByPartition<Proposal>(SlackHelper.GetPartitionKey(parameters["team_id"], parameters["channel_id"]));

            var dialogRequest = new OpenDialogRequest {
                TriggerId = parameters["trigger_id"],
                Dialog = GetPlanDialog(allProposals)
            };

            await SlackHelper.SlackPost("dialog.open", parameters["team_id"], dialogRequest);

            return Ok();
        }

        [FunctionName("RecordProposal")]
        public static async Task RecordProposal(
            [QueueTrigger("proposals")] DialogSubmissionPayload proposalPayload,
            [Table("proposals")] IAsyncCollector<Proposal> proposals,
            ILogger log)
        {
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
                        Title = $"<{proposal.Url}|{proposal.Name}>",
                        Text = proposal.Notes,
                        Footer = "Utilisez /dev-list pour voir toutes les propositions"
                    }
                }
            });
        }

        [FunctionName("RecordPlan")]
        public static async Task RecordPlan(
            [QueueTrigger("plans")] DialogSubmissionPayload planPayload,
            [Table("plans")] IAsyncCollector<Plan> plans,
            ILogger log, IBinder binder)
        {
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
                Attachments = await GetPlanAttachments(binder, plan)
            };

            await SlackHelper.SlackPost("chat.postMessage", planPayload.Team.Id, message);
        }

        [FunctionName("ProcessPlanAction")]
        public static async Task ProcessPlanAction(
            [QueueTrigger("plan-actions")] InteractiveMessagePayload payload,
            [Table("plans")] CloudTable plans,
            ILogger log, IBinder binder)
        {
            var action = payload.Actions.First();
            var plan = await plans.Retrieve<Plan>(payload.PartitionKey, action.Value);

            if (action.Name == "volunteer")
            {
                if (plan.Owner != null)
                {
                    await SlackHelper.SlackPost("chat.postEphemeral", payload.Team.Id, new PostEphemeralMessageRequest {
                        Text = $"<@{plan.Owner}> est déjà responsable de ce Lunch & Watch.",
                        Attachments = new List<MessageAttachment> {
                            GetRemoveMessageAttachment()
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
                        GetRemoveMessageAttachment()
                    }
                });
            }

            var message = new UpdateMessageRequest {
                Text = payload.OriginalMessage.Text,
                Channel = payload.Channel.Id,
                Timestamp = payload.MessageTimestamp,
                Attachments = await GetPlanAttachments(binder, plan)
            };

            await SlackHelper.SlackPost("chat.update", payload.Team.Id, message);
        }

        private static async Task<IActionResult> ProcessProposalAction(IBinder binder,
            InteractiveMessagePayload payload)
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
                var message = GetListMessage(allProposals, payload.Channel.Id);
                message.ReplaceOriginal = true;
                return Ok(message);
            }

            return Ok();
        }

        private static SlackMessage GetListMessage(IList<Proposal> allProposals, string channel)
        {
            SlackMessage message;

            if (allProposals.Any())
            {
                message = new SlackMessage {
                    Text = $"Voici les propositions actuelles pour <#{channel}>:",
                    Attachments = allProposals.Select(GetProposalAttachment).ToList()
                };
            }
            else
            {
                message = new SlackMessage {
                    Text = $"Il n'y a aucune proposition pour le moment dans <#{channel}>."
                };
            }

            message.Attachments.Add(GetRemoveMessageAttachment());
            return message;
        }

        private static MessageAttachment GetProposalAttachment(Proposal p)
        {
            return new MessageAttachment {
                AuthorName = $"Proposé par <@{p.ProposedBy}>",
                Title = $"<{p.Url}|{p.Name}>",
                Text = p.Notes,
                CallbackId = "proposal_action",
                Actions = new List<MessageAction> {
                    new MessageAction {
                        Type = "button",
                        Name = "delete",
                        Text = "Supprimer",
                        Value = p.RowKey,
                        Style = "danger",
                        Confirm = new ActionConfirmation {
                            Title = "Supprimer la proposition",
                            Text = $"Voulez-vous vraiment supprimer la proposition \"{p.Name}\" ?",
                            OkText = "Supprimer",
                            DismissText = "Annuler"
                        }
                    }
                }
            };
        }

        private static MessageAttachment GetRemoveMessageAttachment()
        {
            return new MessageAttachment {
                CallbackId = "message_action",
                Actions = new List<MessageAction> {
                    new MessageAction {
                        Type = "button",
                        Text = "Fermer ce message",
                        Name = "removeme"
                    }
                }
            };
        }

        private static async Task<IList<MessageAttachment>> GetPlanAttachments(IBinder binder, Plan plan)
        {
            Proposal proposal = null;
            if (!string.IsNullOrWhiteSpace(plan.Video))
            {
                var proposals = await binder.GetTable("proposals");
                proposal = await proposals.Retrieve<Proposal>(plan.PartitionKey, plan.Video);
            }

            var result = new List<MessageAttachment> {
                new MessageAttachment {
                    Title = plan.Date.ToString("'Le' dddd d MMMM", CultureInfo.GetCultureInfo("fr-CA")),
                    Fields = new List<AttachmentField> {
                        new AttachmentField {
                            Title = "Responsable",
                            Value = string.IsNullOrWhiteSpace(plan.Owner) ? "À déterminer" : $"<@{plan.Owner}>",
                            Short = true
                        },
                        new AttachmentField {
                            Title = "Video",
                            Value = proposal == null ? "À déterminer" : $"<{proposal.Url}|{proposal.Name}>",
                            Short = true
                        }
                    }
                }
            };

            if (string.IsNullOrWhiteSpace(plan.Owner))
            {
                result.Add(new MessageAttachment {
                    CallbackId = "plan_action",
                    Actions = new List<MessageAction> {
                        new MessageAction {
                            Type = "button",
                            Name = "volunteer",
                            Value = plan.RowKey,
                            Text = "Je m'en occupe",
                            Confirm = new ActionConfirmation {
                                Title = "Confirmer",
                                Text = "Vous confirmez que vous êtes responsable de ce Lunch & Watch ?",
                                OkText = "Oui!",
                                DismissText = "Oups, non"
                            }
                        }
                    }
                });
            }

            return result;
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
            var now = GetLocalNow();

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

        private static DateTime GetLocalNow()
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
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

        private static Dialog GetPlanDialog(IList<Proposal> allProposals)
        {
            var dialog = new Dialog {
                CallbackId = "plan",
                Title = "Planifier un Lunch&Watch",
                SubmitLabel = "Planifier",
                Elements = new List<DialogElement> {
                    new TextDialogElement("date", "Date") {
                        Hint = "Au format AAAA-MM-JJ"
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
                        .Select(x => new SelectOption {
                            Label = x.Name,
                            Value = x.RowKey
                        }).ToArray(),
                    Hint = "Si non choisi, le bot va faire voter le channel."
                });
            }

            return dialog;
        }

        private static IActionResult Ok() => new OkResult();
        private static IActionResult Ok(object obj)
        {
            return new OkObjectResult(obj) {
                Formatters = new FormatterCollection<IOutputFormatter> {
                    new JsonOutputFormatter(new JsonSerializerSettings {
                        NullValueHandling = NullValueHandling.Ignore
                    }, ArrayPool<char>.Shared)
                }
            };
        }
    }
}
