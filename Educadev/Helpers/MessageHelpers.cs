using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Educadev.Models.Slack.Messages;
using Educadev.Models.Slack.Payloads;
using Educadev.Models.Tables;
using Microsoft.Azure.WebJobs;

namespace Educadev.Helpers
{
    public class MessageHelpers
    {
        public static async Task<SlackMessage> GetListMessage(IBinder binder, IList<Proposal> allProposals, string channel)
        {
            SlackMessage message;

            if (allProposals.Any())
            {
                var attachmentTasks = allProposals
                    .OrderBy(x => x.Name)
                    .Select(x => GetProposalAttachment(binder, x));

                message = new SlackMessage {
                    Text = $"Voici les propositions actuelles pour <#{channel}>:",
                    Attachments = (await Task.WhenAll(attachmentTasks)).ToList()
                };
            }
            else
            {
                message = new SlackMessage {
                    Text = $"Il n'y a aucune proposition pour le moment dans <#{channel}>. Utilisez `/edu:propose` pour ajouter des vidéos!"
                };
            }

            message.Attachments.Add(GetRemoveMessageAttachment());
            return message;
        }

        public static async Task<MessageAttachment> GetPlanAttachment(IBinder binder, Plan plan)
        {
            Proposal proposal = null;
            if (!string.IsNullOrWhiteSpace(plan.Video))
            {
                var proposals = await binder.GetTable("proposals");
                proposal = await proposals.Retrieve<Proposal>(plan.PartitionKey, plan.Video);
            }

            var result = new MessageAttachment {
                PreText = $"*Le {plan.Date:dddd d MMMM}*",
                Color = "#004492",
                Fields = new List<AttachmentField> {
                    new AttachmentField {
                        Title = "Responsable",
                        Value = string.IsNullOrWhiteSpace(plan.Owner) ? "À déterminer" : $"<@{plan.Owner}>",
                        Short = true
                    },
                    new AttachmentField {
                        Title = "Video",
                        Value = proposal == null ? "À déterminer" : proposal.GetFormattedTitle(),
                        Short = true
                    }
                },
                CallbackId = "plan_action"
            };

            if (string.IsNullOrWhiteSpace(plan.Owner))
            {
                result.Actions = new List<MessageAction> {
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
                };
            }

            return result;
        }

        public static async Task<MessageAttachment> GetProposalAttachment(IBinder binder, Proposal proposal, bool allowActions = true)
        {
            var attachment = new MessageAttachment {
                AuthorName = $"Proposé par <@{proposal.ProposedBy}>",
                Title = proposal.GetFormattedTitle(),
                Text = proposal.Notes,
                Color = "#1d7c00",
            };

            if (allowActions)
            {
                Plan plan = null;
                if (!string.IsNullOrWhiteSpace(proposal.PlannedIn))
                    plan = await binder.GetTableRow<Plan>("plans", proposal.PartitionKey, proposal.PlannedIn);

                if (plan == null)
                {
                    attachment.CallbackId = "proposal_action";
                    attachment.Actions = new List<MessageAction> {
                        new MessageAction {
                            Type = "button",
                            Name = "delete",
                            Text = "Supprimer",
                            Value = proposal.RowKey,
                            Style = "danger",
                            Confirm = new ActionConfirmation {
                                Title = "Supprimer la proposition",
                                Text = $"Voulez-vous vraiment supprimer la proposition \"{proposal.Name}\" ?",
                                OkText = "Supprimer",
                                DismissText = "Annuler"
                            }
                        }
                    };
                }
                else
                {
                    attachment.Footer = $"Planifié pour le {plan.Date:dddd d MMMM}.";
                }
            }

            return attachment;
        }

        public static MessageAttachment GetRemoveMessageAttachment()
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

        public static async Task PostErrorMessage(Payload payload)
        {
            var message = new PostEphemeralRequest {
                User = payload.User.Id,
                Channel = payload.Channel.Id,
                Text = "Oups! Il y a eu un problème. Ré-essayez ?",
                Attachments = {GetRemoveMessageAttachment()}
            };

            await SlackHelper.SlackPost("chat.postEphemeral", payload.Team.Id, message);
        }
    }
}