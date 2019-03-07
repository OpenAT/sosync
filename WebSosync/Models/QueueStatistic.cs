using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Models
{
    public class QueueStatistic
    {
        public int TotalJobs { get; set; }
        public int SubmittedJobs { get; set; }
        public int Difference { get { return TotalJobs - SubmittedJobs; } }
    }
}
