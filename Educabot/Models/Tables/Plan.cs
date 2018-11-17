using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Educabot.Models.Tables
{
    public class Plan : TableEntity
    {
        public string CreatedBy { get; set; }
        public string Team { get; set; }
        public string Channel { get; set; }

        public DateTime Date { get; set; }
        public string Owner { get; set; } = "";
        public string Video { get; set; } = "";
    }
}