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
                    Attachments = await MessageHelpers.GetPlanAttachments(binder, plan)
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
                    Attachments = await MessageHelpers.GetPlanAttachments(binder, plan)
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
                    Attachments = await MessageHelpers.GetPlanAttachments(binder, plan)
                });
            }
        }

        [FunctionName("CancelOrphanPlans")]
        public static async Task CancelOrphanPlans(
            [TimerTrigger("0 05 12 * * *", RunOnStartup = true)] TimerInfo timer, // 12:05AM daily
            [Table("plans")] CloudTable plansTable,
            IBinder binder)
        {
            var plans = await GetTodayPlansWithoutResponsible(plansTable);

            foreach (var plan in plans)
            {
                var result = await plansTable.ExecuteAsync(TableOperation.Delete(plan));
                if (result.IsError()) continue;

                await SlackHelper.SlackPost("chat.postMessage", plan.Team, new PostMessageRequest {
                    Channel = plan.Channel,
                    Text = "Le Lunch & Watch de ce midi a été annulé car aucun responsable ne s'est manifesté."
                });
            }
        }

        private static async Task<IList<Plan>> GetTodayPlansWithoutResponsible(CloudTable plansTable)
        {
            var query = new TableQuery<Plan>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterConditionForDate("Date", "eq", DateTime.Today),
                    "and",
                    TableQuery.GenerateFilterCondition("Owner", "eq", null)
                ));

            var plans = await plansTable.ExecuteQueryAsync(query);
            return plans;
        }
    }
}