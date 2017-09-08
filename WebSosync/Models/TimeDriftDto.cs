using Syncer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Models
{
    public class TimeDriftDto
    {
        public TimeDrift Drift { get; set; }
        public int Tolerance { get; set; }
        public string Unit { get; set; }
    }
}
