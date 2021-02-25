using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Models
{
    public class ThreadSettingsDto
    {
        public int? Threads { get; set; }
        public int? PackageSize { get; set; }
        public int? ActiveSeconds { get; set; }
    }
}
