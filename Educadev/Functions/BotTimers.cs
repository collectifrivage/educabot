using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        
        [FunctionName("PlanResponsibleReminder")]
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

        [FunctionName("PlanReminder")]
        public static async Task PlanReminder(
            [TimerTrigger("0 0 9 * * *")] TimerInfo timer, // 9:00AM daily
            [Table("plans")] CloudTable plansTable,
            IBinder binder)
        {
            var today = DateTime.Today;
            var withoutVideo = TableQuery.GenerateFilterCondition("Video", "eq", "");

            // Lundi: Initier le vote pour les vidéos de cette semaine (sauf si aujourd'hui)
            if (today.DayOfWeek == DayOfWeek.Monday)
            {
                var weeksPlans = await PlanHelpers.GetPlansBetween(plansTable, today.AddDays(1), today.AddDays(5), withoutVideo);
                foreach (var plan in weeksPlans)
                    await SendMessage(plan);
            }
            // Vendredi: Initier le vote pour les vidéos de lundi prochain
            if (today.DayOfWeek == DayOfWeek.Friday)
            {
                var mondaysPlans = await PlanHelpers.GetPlansForDate(plansTable, today.AddDays(3), withoutVideo);
                foreach (var plan in mondaysPlans)
                    await SendMessage(plan);
            }

            // Rappel du lunch & watch d'aujourd'hui
            var todaysPlans = await PlanHelpers.GetPlansForDate(plansTable, today);
            foreach (var plan in todaysPlans)
                await SendMessage(plan);

            async Task SendMessage(Plan plan)
            {
                var isToday = plan.Date.Date == today;

                string message = null;

                if (isToday)
                {
                    message = "Rappel: Il y a un Lunch & Watch ce midi!";
                    if (string.IsNullOrWhiteSpace(plan.Video))
                        message += " Votez pour le vidéo si ce n'est pas déjà fait! Vous avez jusqu'à 11h15 pour choisir.";
                    if (string.IsNullOrWhiteSpace(plan.Owner))
                        message += "\n\nIl n'y a pas encore de responsable pour préparer le vidéo. Cliquez sur _Je m'en occupe_ pour vous porter volontaire!";
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(plan.Video))
                        message = "C'est le moment de voter pour le vidéo de ce Lunch & Watch :";
                }

                if (string.IsNullOrWhiteSpace(message)) return;

                await SlackHelper.SlackPost("chat.postMessage", plan.Team, new PostMessageRequest {
                    Channel = plan.Channel,
                    Text = message,
                    Attachments = {await MessageHelpers.GetPlanAttachment(binder, plan)}
                });
            }
        }

        [FunctionName("CloseVote")]
        public static async Task CloseVote(
            [TimerTrigger("0 15 11 * * *")] TimerInfo timer, // 11:15AM daily
            [Table("plans")] CloudTable plansTable,
            [Table("votes")] CloudTable votesTable,
            [Table("proposals")] CloudTable proposalsTable)
        {
            var today = DateTime.Today;
            var withoutVideo = TableQuery.GenerateFilterCondition("Video", "eq", "");
            
            var plans = await PlanHelpers.GetPlansForDate(plansTable, today, withoutVideo);
            foreach (var plan in plans)
            {
                await CloseVote(plansTable, votesTable, proposalsTable, plan);
            }
        }

        private static async Task CloseVote(CloudTable plansTable, CloudTable votesTable, CloudTable proposalsTable, Plan plan)
        {
            var votes = await votesTable.GetAllByPartition<Vote>(
                Utils.GetPartitionKeyWithAddon(plan.PartitionKey, plan.RowKey));
            
            var rng = new Random();
            var results = votes
                .SelectMany(x => new[] {
                    new {video = x.Proposal1, score = 5},
                    new {video = x.Proposal2, score = 3},
                    new {video = x.Proposal3, score = 1}
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.video))
                .GroupBy(x => x.video)
                .Select(g => new {video = g.Key, score = g.Sum(x => x.score), count = g.Count()})
                .OrderByDescending(x => x.score)
                .ThenBy(x => rng.Next())
                .ToList();

            if (!results.Any())
            {
                await plansTable.ExecuteAsync(TableOperation.Delete(plan));

                var failMessage = new PostMessageRequest {
                    Channel = plan.Channel,
                    Text = "Comme personne n'a voté pour le Lunch & Watch de ce midi, ce dernier a été annulé."
                };
                await SlackHelper.SlackPost("chat.postMessage", plan.Team, failMessage);
                return;
            }

            plan.Video = results.First().video;
            await plansTable.ExecuteAsync(TableOperation.Replace(plan));

            var proposal = await proposalsTable.Retrieve<Proposal>(plan.PartitionKey, plan.Video);
            proposal.PlannedIn = plan.RowKey;
            await proposalsTable.ExecuteAsync(TableOperation.Replace(proposal));

            var message = new PostMessageRequest {
                Channel = plan.Channel,
                Text = ":trophy: Voici le résultat du vote pour le Lunch & Watch de ce midi :"
            };

            for (var i = 0; i < 3; i++)
            {
                if (results.Count <= i) break;
                var result = results[i];
                
                var prop = await proposalsTable.Retrieve<Proposal>(plan.PartitionKey, result.video);
                message.Attachments.Add(new MessageAttachment {
                    Title = FormatPosition(i), 
                    Text = prop.GetFormattedTitle(), 
                    Footer = $"{Pluralize(result.score, "point")}, {Pluralize(result.count, "vote")}", 
                    Color = GetColor(i)
                });
            }

            if (results.Count > 1 && results[0].score == results[1].score)
            {
                message.Attachments.Add(new MessageAttachment {
                    Text = "Note: comme il y avait égalité pour la première position, l'ordre a été déterminé au hasard."
                });
            }

            await SlackHelper.SlackPost("chat.postMessage", plan.Team, message);

            string FormatPosition(int index) =>
                index == 0 ? ":first_place_medal: Première position" :
                index == 1 ? ":second_place_medal: Deuxième position" :
                index == 2 ? ":third_place_medal: Troisième position" : null;

            string GetColor(int index) =>
                index == 0 ? "#FFD700" :
                index == 1 ? "#C0C0C0" :
                index == 2 ? "#CD7F32" : null;
        }

        private static string Pluralize(int n, string name) => $"{n} {name}{(n > 1 ? "s" : "")}";

        private static async Task<IList<Plan>> GetTodayPlansWithoutResponsible(CloudTable plansTable)
        {
            var filter = TableQuery.GenerateFilterCondition("Owner", "eq", null);
            return await PlanHelpers.GetPlansForDate(plansTable, DateTime.Today, filter);
        }

        private static async Task<IList<Plan>> GetTodayPlansWithVideoAndResponsible(CloudTable plansTable)
        {
            var filter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("Owner", "ne", null),
                "and",
                TableQuery.GenerateFilterCondition("Video", "ne", null)
            );

            return await PlanHelpers.GetPlansForDate(plansTable, DateTime.Today, filter);
        }
    }
}