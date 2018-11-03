using Microsoft.WindowsAzure.Storage.Table;

namespace Educadev.Models.Tables
{
    public class Proposal : TableEntity
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Notes { get; set; }
        public string ProposedBy { get; set; }

        public string GetFormattedTitle() => Url.StartsWith("http") ? $"<{Url}|{Name}>" : Name;
    }
}