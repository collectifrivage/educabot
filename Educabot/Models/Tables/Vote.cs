using Educabot.Helpers;
using Microsoft.WindowsAzure.Storage.Table;

namespace Educabot.Models.Tables
{
    public class Vote : TableEntity
    {
        public string Proposal1 { get; set; }
        public string Proposal2 { get; set; }
        public string Proposal3 { get; set; }

        public Vote(string teamId, string channelId, string planId, string userId)
        {
            PartitionKey = Utils.GetPartitionKey(teamId, channelId, planId);
            RowKey = userId;
        }

        public Vote() {}
    }
}