using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Educadev.Models.Tables;
using Microsoft.WindowsAzure.Storage.Table;

namespace Educadev.Helpers
{
    public static class ProposalHelpers
    {
        public static async Task<IList<Proposal>> GetActiveProposals(CloudTable proposals, string partitionKey)
        {
            var query = new TableQuery<Proposal>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", "eq", partitionKey),
                    "and",
                    TableQuery.GenerateFilterConditionForBool("Complete", "eq", false)
                ));

            return await proposals.ExecuteQueryAsync(query);
        }
    }

    public static class PlanHelpers
    {
        public static async Task<IList<Plan>> GetPlansBetween(CloudTable plans, DateTime start, DateTime end, string additionalFilter = null)
        {
            var filter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterConditionForDate("Date", "ge", start),
                "and",
                TableQuery.GenerateFilterConditionForDate("Date", "lt", end)
            );

            if (!string.IsNullOrWhiteSpace(additionalFilter))
                filter = TableQuery.CombineFilters(filter, "and", additionalFilter);

            var query = new TableQuery<Plan>().Where(filter);

            return await plans.ExecuteQueryAsync(query);
        }

        public static Task<IList<Plan>> GetPlansForDate(CloudTable plans, DateTime date, string additionalFilter = null)
        {
            return GetPlansBetween(plans, date.Date, date.Date.AddDays(1), additionalFilter);
        }

        public static async Task<Plan> GetPlanForDate(CloudTable plans, string partitionKey, DateTime date, string additionalFilter = null)
        {
            var filter = TableQuery.GenerateFilterCondition("PartitionKey", "eq", partitionKey);
            if (!string.IsNullOrWhiteSpace(additionalFilter))
                filter = TableQuery.CombineFilters(filter, "and", additionalFilter);

            var results = await GetPlansBetween(plans, date.Date, date.Date.AddDays(1), filter);

            return results.SingleOrDefault();
        }
    }
}