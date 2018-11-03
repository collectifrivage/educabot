using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Educadev.Models.Slack.Messages;
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

        public static async Task<IList<MessageAttachment>> GetPlanAttachments(IBinder binder, Plan plan)
        {
            Proposal proposal = null;
            if (!string.IsNullOrWhiteSpace(plan.Video))
            {
                var proposals = await binder.GetTable("proposals");
                proposal = await proposals.Retrieve<Proposal>(plan.PartitionKey, plan.Video);
            }

            var result = new List<MessageAttachment> {
                new MessageAttachment {
                    Title = plan.Date.ToString("'Le' dddd d MMMM"),
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
                    Footer = (proposal == null ? "Proposez des vidéos avec /edu:propose! " : "") +
                             "Utilisez /edu:next pour voir le prochain Lunch & Watch planifié."
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

        public static async Task<MessageAttachment> GetProposalAttachment(IBinder binder, Proposal proposal, bool allowActions = true)
        {
            var attachment = new MessageAttachment {
                AuthorName = $"Proposé par <@{proposal.ProposedBy}>",
                Title = proposal.GetFormattedTitle(),
                Text = proposal.Notes
            };

            if (allowActions)
            {
                if (string.IsNullOrWhiteSpace(proposal.PlannedIn))
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
                    var plan = await binder.GetTableRow<Plan>("plans", proposal.PartitionKey, proposal.PlannedIn);
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
    }
}