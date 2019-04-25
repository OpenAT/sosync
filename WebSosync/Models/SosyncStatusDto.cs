using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using WebSosync.Data.Models;

namespace WebSosync.Models
{
    [DataContract(Name = "sosync_status")]
    public class SosyncStatusDto
    {
        #region Properties
        [DataMember(Name = "job_worker")]
        public ServiceStatusDto JobWorker { get; set; }
        [DataMember(Name = "thread_pool")]
        public ThreadStatusDto ThreadPool { get; set; }
        #endregion

        #region Constructors
        public SosyncStatusDto()
        {
            JobWorker = new ServiceStatusDto();
            ThreadPool = new ThreadStatusDto();
        }
        #endregion
    }
}
