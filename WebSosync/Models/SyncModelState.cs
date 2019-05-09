using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Models
{
    public class SyncModelState
    {
        public bool InSync { get; set; }
        public int JobErrors { get; set; }
        public int JobErrorsRetry { get; set; }
        public string Information { get; set; }
    }
}
