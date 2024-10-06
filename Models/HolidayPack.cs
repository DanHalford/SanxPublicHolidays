using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PublicHolidays.Models
{
    internal class HolidayPack
    {
        public Guid id { get; set; }
        public string category { get; set; } = default!;
        public Holiday[] holidays { get; set; } = default!;
    }
}
