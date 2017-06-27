using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace WebSosync.Models
{
    /// <summary>
    /// Represents a sync job data transfer object (DTO) to be used
    /// by webservice controllers.
    /// </summary>
    [DataContract(Name = "sync_job")]
    public class SyncJobDto
    {
        [DataMember(Name = "source_system")]
        public string Source_System { get; set; }

        [DataMember(Name = "source_model")]
        public string Source_Model { get; set; }

        [DataMember(Name = "source_record_id")]
        public string Source_Record_ID { get; set; }

        /// <summary>
        /// The actual job creation date. Use UTC time.
        /// </summary>
        [DataMember(Name = "job_date")]
        public DateTime Job_Date { get; set; }
    }
}
