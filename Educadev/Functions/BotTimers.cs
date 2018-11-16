using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Educadev.Helpers;
using Educadev.Models.Slack.Messages;
using Educadev.Models.Tables;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Table;

namespace Educadev.Functions
{
    public static class BotTimers
    {
        static BotTimers()
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-CA");
        }

        [FunctionName("PlanResponsibleFirstReminder")]
        public static async Task PlanResponsibleFirstReminder(
            [TimerTrigger("0 0 9 * * *")] TimerInfo timer, // 9AM daily
            [Table("plans")] CloudTable plansTable,
            IBinder binder)
        {
            var plans = await GetTodayPlansWithoutResponsible(plansTable);

            foreach (var plan in plans)
            {
                await SlackHelper.SlackPost("chat.postMessage", plan.Team, new PostMessageRequest {
                    Channel = plan.Channel,
                    Text = "Le Lunch & Watch de ce midi a besoin d'un responsable!",
                    Attachments = {await MessageHelpers.GetPlanAttachment(binder, plan)}
                });
            }
        }

        [FunctionName("PlanResponsibleSecondReminder")]
        public static async Task PlanResponsibleSecondReminder(
            [TimerTrigger("0 0 11 * * *")] TimerInfo timer, // 11AM daily
            [Table("plans")] CloudTable plansTable,
            IBinder binder)
        {
            var plans = await GetTodayPlansWithoutResponsible(plansTable);

            foreach (var plan in plans)
            {
                await SlackHelper.SlackPost("chat.postMessage", plan.Team, new PostMessageRequest {
                    Channel = plan.Channel,
                    Text = "<!channel> Rappel: Le Lunch & Watch de ce midi a besoin d'un responsable!",
                    Attachments = {await MessageHelpers.GetPlanAttachment(binder, plan)}
                });
            }
        }

        [FunctionName("PlanResponsibleFinalReminder")]
        public static async Task PlanResponsibleFinalReminder(
            [TimerTrigger("0 55 11 * * *")] TimerInfo timer, // 11:55AM daily
            [Table("plans")] CloudTable plansTable,
            IBinder binder)
        {
            var plans = await GetTodayPlansWithoutResponsible(plansTable);

            foreach (var plan in plans)
            {
                await SlackHelper.SlackPost("chat.postMessage", plan.Team, new PostMessageRequest {
                    Channel = plan.Channel,
                    Text = "<!channel> *Dernier rappel*: Le Lunch & Watch de ce midi a besoin d'un responsable! Si personne ne se manifeste, l'événement sera annulé.",
                    Attachments = {await MessageHelpers.GetPlanAttachment(binder, plan)}
                });
            }
        }

        [FunctionName("CancelOrphanPlans")]
        public static async Task CancelOrphanPlans(
            [TimerTrigger("0 05 12 * * *")] TimerInfo timer, // 12:05AM daily
            [Table("plans")] CloudTable plansTable,
            [Table("proposals")] CloudTable proposalsTable,
            IBinder binder)
        {
            var plans = await GetTodayPlansWithoutResponsible(plansTable);

            foreach (var plan in plans)
            {
                if (!string.IsNullOrWhiteSpace(plan.Video))
                {
                    var proposal = await proposalsTable.Retrieve<Proposal>(plan.PartitionKey, plan.Video);
                    if (proposal != null)
                    {
                        proposal.PlannedIn = "";
                        await proposalsTable.ExecuteAsync(TableOperation.Replace(proposal));
                    }
                }

                var result = await plansTable.ExecuteAsync(TableOperation.Delete(plan));
                if (result.IsError()) continue;

                await SlackHelper.SlackPost("chat.postMessage", plan.Team, new PostMessageRequest {
                    Channel = plan.Channel,
                    Text = "Le Lunch & Watch de ce midi a été annulé car aucun responsable ne s'est manifesté."
                });
            }
        }

        [FunctionName("ClompletePlans")]
        public static async Task CompletePlans(
            [TimerTrigger("0 0 13 * * *")] TimerInfo timer, // 1:00PM daily
            [Table("plans")] CloudTable plansTable,
            [Table("proposals")] CloudTable proposalsTable)
        {
            var plans = await GetTodayPlansWithVideoAndResponsible(plansTable);

            foreach (var plan in plans)
            {
                var proposal = await proposalsTable.Retrieve<Proposal>(plan.PartitionKey, plan.Video);

                proposal.Complete = true;
                await proposalsTable.ExecuteAsync(TableOperation.Replace(proposal));

                await SlackHelper.SlackPost("chat.postMessage", plan.Team, new PostMessageRequest {
                    Channel = plan.Owner,
                    Text = $"Avez-vous terminé l'écoute du vidéo _{proposal.Name}_?\nSi vous choisissez Non, alors le vidéo sera automatiquement re-proposé pour le continuer plus tard.",
                    Attachments = {
                        new MessageAttachment {
                            CallbackId = "proposal_action",
                            Actions = new List<MessageAction>{
                                new MessageAction {Type = "button", Text = "Oui, le vidéo est terminé", Name = "done", Value = $"{plan.PartitionKey}/{plan.Video}"},
                                new MessageAction {Type = "button", Text = "Non, pas encore", Name = "incomplete", Value = $"{plan.PartitionKey}/{plan.Video}"}
                            }
                        }
                    }
                });
            }
        }

        [FunctionName("PublishVote")]
        public static async Task PublishVote(
            [TimerTrigger("0 15 9 * * 1-5")] TimerInfo timer, // 9:15AM monday-friday
            [Table("plans")] CloudTable plansTable)
        {
            // TODO Lundi: Initier le vote pour les vidéos de cette semaine (sauf si aujourd'hui)
            // TODO Vendredi: Initier le vote pour les vidéos de lundi prochain
            // TODO Tous les jours: Rappel du vote pour les vidéos d'aujourd'hui même
        }

        private static async Task<IList<Plan>> GetTodayPlansWithoutResponsible(CloudTable plansTable)
        {
            var query = new TableQuery<Plan>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterConditionForDate("Date", "eq", DateTime.Today.AddHours(12)),
                    "and",
                    TableQuery.GenerateFilterCondition("Owner", "eq", null)
                ));

            var plans = await plansTable.ExecuteQueryAsync(query);
            return plans;
        }

        private static async Task<IList<Plan>> GetTodayPlansWithVideoAndResponsible(CloudTable plansTable)
        {
            var query = new TableQuery<Plan>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterConditionForDate("Date", "eq", DateTime.Today.AddHours(12)),
                    "and",
                    TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition("Owner", "ne", null),
                        "and",
                        TableQuery.GenerateFilterCondition("Video", "ne", null)
                    )
                ));

            var plans = await plansTable.ExecuteQueryAsync(query);
            return plans;
        }
    }
}