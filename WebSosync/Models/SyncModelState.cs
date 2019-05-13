using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Models
{
    public class SyncModelState
    {
        public bool InSync { get; set; }
        public bool HasOpenJobs { get; set; }
        public string Information { get; set; }
    }
}
