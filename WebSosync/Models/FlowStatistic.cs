using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Models
{
    public class FlowStatistic
    {
        public FlowStatistic()
        {
            UnsynchronizedModels = new Dictionary<string, int>();
        }

        public Dictionary<string, int> UnsynchronizedModels { get; set; }
    }
}
