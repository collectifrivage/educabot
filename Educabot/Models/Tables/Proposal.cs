using Microsoft.WindowsAzure.Storage.Table;

namespace Educabot.Models.Tables
{
    public class Proposal : TableEntity
    {
        public string ProposedBy { get; set; }
        public string Team { get; set; }
        public string Channel { get; set; }

        public string Name { get; set; }
        public int Part { get; set; } = 1;
        public string Url { get; set; }
        public string Notes { get; set; }

        public string PlannedIn { get; set; }

        public bool Complete { get; set; }

        public string GetFormattedTitle()
        {
            var result = Url.StartsWith("http") ? $"<{Url}|{Name}>" : Name;
            if (Part > 1)
                result += $" [{Part}e partie]";

            return result;
        }
    }
}