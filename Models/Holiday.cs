using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PublicHolidays.Models
{
    internal class Holiday
    {
        public string name { get; set; } = default!;
        public DateOnly date { get; set; }
        public string[] location { get; set; } = default!;
        public string category { get; set; } = default!;
        public string info { get; set; } = default!;
        public bool remove { get; set; } = false;
    }
}
