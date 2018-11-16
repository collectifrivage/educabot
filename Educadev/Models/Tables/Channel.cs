using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Educadev.Models.Tables
{
    public class Channel : TableEntity
    {
        public DateTime LastActivity { get; set; }

        public Channel() {}
        public Channel(string teamId, string channelId) : base(teamId, channelId)
        {
            LastActivity = DateTime.Now;
        }
    }
}