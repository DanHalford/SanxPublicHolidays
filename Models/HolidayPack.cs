namespace PublicHolidays.Models
{
    internal class HolidayPack
    {
        public Guid id { get; set; }
        public string category { get; set; } = default!;
        public Holiday[] holidays { get; set; } = default!;
    }
}
