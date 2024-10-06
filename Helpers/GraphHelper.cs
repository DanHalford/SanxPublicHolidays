using Azure.Identity;
using Microsoft.Graph;

namespace PublicHolidays.Helpers
{
    internal class GraphHelper
    {
        public static GraphServiceClient GetGraphServiceClient()
        {
            var credential = new DefaultAzureCredential();
            var graphClient = new GraphServiceClient(credential, new[] { "https://graph.microsoft.com/.default" });

            return graphClient;
        }
    }
}
