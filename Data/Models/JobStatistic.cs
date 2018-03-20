using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data.Models
{
    public class JobStatistic
    {
        public long total { get; set; }
        public long @new { get; set; }
        public long in_progress { get; set; }
        public long error { get; set; }
        public long done { get; set; }
    }
}
