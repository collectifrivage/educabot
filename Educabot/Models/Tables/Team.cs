using Microsoft.WindowsAzure.Storage.Table;

namespace Educabot.Models.Tables
{
    public class Team : TableEntity
    {
        public string AccessToken { get; set; }

        public Team() {}
        public Team(string teamId, string accessToken) : base("teams", teamId)
        {
            AccessToken = accessToken;
        }
    }
}
