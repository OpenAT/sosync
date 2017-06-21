using System;
using System.Collections.Generic;
using System.Text;

namespace WebSosync.Data.Models
{
    public class SosyncJobQueue : SosyncJob
    {
        public DateTime? job_fetched { get; set; }
    }
}
