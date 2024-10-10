using PublicHolidays.Models;

namespace PublicHolidays.Helpers
{
    internal class EnvironmentVariableHelper
    {
        public static AzureAuthenticationModel GetAzureAuthenticationCredentials()
        {
            return new AzureAuthenticationModel
            {
                ClientId = Environment.GetEnvironmentVariable("ClientId") ?? throw new ArgumentNullException("ClientId"),
                ClientSecret = Environment.GetEnvironmentVariable("ClientSecret") ?? throw new ArgumentNullException("ClientSecret"),
                TenantId = Environment.GetEnvironmentVariable("TenantId") ?? throw new ArgumentNullException("TenantId")
            };
        }

        public string GetVariable(string variableName)
        {
            return Environment.GetEnvironmentVariable(variableName) ?? throw new ArgumentNullException(variableName);
        }
    }
}
