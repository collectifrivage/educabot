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
        public static SlackMessage GetListMessage(IList<Proposal> allProposals, string channel)
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
                    Title = plan.Date.ToString("'Le' dddd d MMMM", CultureInfo.GetCultureInfo("fr-CA")),
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

        public static MessageAttachment GetProposalAttachment(Proposal p)
        {
            return new MessageAttachment {
                AuthorName = $"Proposé par <@{p.ProposedBy}>",
                Title = p.GetFormattedTitle(),
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