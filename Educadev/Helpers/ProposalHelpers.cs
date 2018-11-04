using System.Collections.Generic;
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
}