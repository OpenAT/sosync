using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace WebSosync.Models
{
    [DataContract(Name = "sosync_status")]
    public class SosyncStatusDto
    {
        #region Properties
        [DataMember(Name = "job_worker")]
        public ServiceStatusDto JobWorker { get; set; }

        [DataMember(Name = "protocol_worker")]
        public ServiceStatusDto ProtocolWorker { get; set; }
        #endregion

        #region Constructors
        public SosyncStatusDto()
        {
            JobWorker = new ServiceStatusDto();
            ProtocolWorker = new ServiceStatusDto();
        }
        #endregion
    }
}
