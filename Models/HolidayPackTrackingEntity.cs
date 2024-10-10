using Azure;
using Azure.Data.Tables;

namespace PublicHolidays.Models
{
    internal class HolidayPackTrackingEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;    //UserMailboxId
        public string RowKey { get; set; } = default!;          //HolidayPackId
        public DateTime AppliedDate { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
