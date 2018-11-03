using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Educadev.Models.Tables
{
    public class Plan : TableEntity
    {
        public DateTime Date { get; set; }
        public string Owner { get; set; }
        public string Video { get; set; }
    }
}