using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace WebSosync.Data.Models
{
    [DataContract(Name = "sync_table")]
    public class SyncJob
    {
        #region Constructors
        public SyncJob()
        {
            Children = new List<SyncJob>();
        }
        #endregion

        #region Properties
        /// <summary>
        /// The sync job ID in the source system.
        /// </summary>
        [DataMember(Name = "job_id")]
        public int Job_ID { get; set; }

        [DataMember(Name = "job_fs_id")]
        [IgnoreDataMember]
        public int? Job_Fs_ID { get; set; }

        [DataMember(Name = "job_fso_id")]
        [IgnoreDataMember]
        public int? Job_Fso_ID { get; set; }

        [DataMember(Name = "job_date")]
        public DateTime Job_Date { get; set; }

        [DataMember(Name = "fetched")]
        public DateTime? Fetched { get; set; }

        [DataMember(Name = "start")]
        public DateTime? Start { get; set; }

        [DataMember(Name = "end")]
        public DateTime? End { get; set; }

        /// <summary>
        /// Use values from <see cref="SosyncState"/> only.
        /// </summary>
        [DataMember(Name = "state")]
        public string State { get; set; }

        /// <summary>
        /// Use values from <see cref="SosyncError"/> only.
        /// </summary>
        [DataMember(Name = "error_code")]
        public string Error_Code { get; set; }

        [DataMember(Name = "parent_job_id")]
        public int? Parent_Job_ID { get; set; }

        [DataMember(Name = "child_start")]
        public DateTime? Child_Start { get; set; }

        [DataMember(Name = "child_end")]
        public DateTime? Child_End { get; set; }

        /// <summary>
        /// Use values from <see cref="SosyncSystem"/> only.
        /// </summary>
        [DataMember(Name = "source_system")]
        public string Source_System { get; set; }

        [DataMember(Name = "source_model")]
        public string Source_Model { get; set; }

        [DataMember(Name = "source_record_id")]
        public int Source_Record_ID { get; set; }

        /// <summary>
        /// Use values from <see cref="SosyncSystem"/> only.
        /// </summary>
        [DataMember(Name = "target_system")]
        public string Target_System { get; set; }

        [DataMember(Name = "target_model")]
        public string Target_Model { get; set; }

        [DataMember(Name = "target_record_id")]
        public int? Target_Record_ID { get; set; }

        [DataMember(Name = "source_data")]
        public string Source_Data { get; set; }

        [DataMember(Name = "target_request")]
        public string Target_Request { get; set; }

        [DataMember(Name = "target_request_start")]
        public DateTime? Target_Request_Start { get; set; }

        [DataMember(Name = "target_request_end")]
        public DateTime? Target_Request_End { get; set; }

        [DataMember(Name = "target_request_answer")]
        public string Target_Request_Answer { get; set; }

        [DataMember(Name = "run_count")]
        public int Run_Count { get; set; }

        [IgnoreDataMember]
        public IList<SyncJob> Children { get; set; }
        #endregion
    }
}
