namespace PublicHolidays.Models
{
    internal class AzureAuthenticationModel
    {
        public string ClientId { get; set; } = default!;
        public string ClientSecret { get; set; } = default!;
        public string TenantId { get; set; } = default!;
    }
}
