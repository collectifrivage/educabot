using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;

namespace Educabot.Helpers
{
    public class ConfigHelper
    {
        private readonly IConfigurationRoot config;

        public ConfigHelper(ExecutionContext context)
        {
            config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }

        public string ClientId => config["ClientId"];
        public string ClientSecret => config["ClientSecret"];
        public string SigningSecret => config["SigningSecret"];
    }
}