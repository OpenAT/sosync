using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace WebSosync.Models
{
    [DataContract(Name = "service_status")]
    public class ServiceStatusDto
    {
        [DataMember(Name = "status")]
        public int Status { get; set; }

        [DataMember(Name = "status_text")]
        public string StatusText { get; set; }
    }
}
