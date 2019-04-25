using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace WebSosync.Models
{
    [DataContract(Name = "thread_status")]
    public class ThreadStatusDto
    {
        #region Properties
        [DataMember(Name = "worker_threads")]
        public int WorkerThreads { get; set; }
        [DataMember(Name = "io_threads")]
        public int IOThreads { get; set; }
        #endregion
    }
}
