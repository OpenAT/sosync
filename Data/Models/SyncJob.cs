using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace WebSosync.Data.Models
{
    /// <summary>
    /// Represents a sync job of sosync. The data member attributes
    /// specify the column names, which are used for the sync_table
    /// aswell as for fso.
    /// 
    /// The ignore data member describe which columns will not b
    /// transmitted to fso.
    /// </summary>
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
        // Sosync only

        [DataMember(Name = "job_fs_id")]
        [IgnoreDataMember]
        public int? Job_Fs_ID { get; set; }

        [DataMember(Name = "job_fso_id")]
        [IgnoreDataMember]
        public int? Job_Fso_ID { get; set; }

        [DataMember(Name = "job_last_change")]
        [IgnoreDataMember]
        public DateTime? Job_Last_Change { get; set; }

        [DataMember(Name = "job_closed_by_job_id")]
        [IgnoreDataMember]
        public int? Job_Closed_By_Job_ID { get; set; }

        [DataMember(Name = "job_to_fso_can_sync")]
        [IgnoreDataMember]
        public bool? Job_To_FSO_Can_Sync { get; set; }

        [DataMember(Name = "job_to_fso_sync_date")]
        [IgnoreDataMember]
        public DateTime? Job_To_FSO_Sync_Date { get; set; }

        [DataMember(Name = "job_to_fso_sync_version")]
        [IgnoreDataMember]
        public DateTime? Job_To_FSO_Sync_Version { get; set; }

        [IgnoreDataMember]
        public List<SyncJob> Children { get; set; }
        
        // SyncJob

        [DataMember(Name = "job_id")]
        public int Job_ID { get; set; }

        [DataMember(Name = "job_date")]
        public DateTime Job_Date { get; set; }

        // SyncJob source

        [DataMember(Name = "job_source_type")]
        public string Job_Source_Type { get; set; }

        [DataMember(Name = "job_source_type_info")]
        public string Job_Source_Type_Info { get; set; }

        [DataMember(Name = "job_source_merge_into_record_id")]
        public int? Job_Source_Merge_Into_Record_ID { get; set; }

        [DataMember(Name = "job_source_target_merge_into_record_id")]
        public int? Job_Source_Target_Merge_Into_Record_ID { get; set; }

        [DataMember(Name = "job_source_system")]
        public string Job_Source_System { get; set; }

        [DataMember(Name = "job_source_model")]
        public string Job_Source_Model { get; set; }

        [DataMember(Name = "job_source_record_id")]
        public int Job_Source_Record_ID { get; set; }

        [DataMember(Name = "job_source_target_record_id")]
        public int? Job_Source_Target_Record_ID { get; set; }

        [DataMember(Name = "job_source_sosync_write_date")]
        public DateTime? Job_Source_Sosync_Write_Date { get; set; }

        [DataMember(Name = "job_source_fields")]
        public string Job_Source_Fields { get; set; }

        // SyncJob info

        [DataMember(Name = "job_fetched")]
        public DateTime? Job_Fetched { get; set; }

        [DataMember(Name = "job_start")]
        public DateTime? Job_Start { get; set; }

        [DataMember(Name = "job_end")]
        public DateTime? Job_End { get; set; }

        [DataMember(Name = "job_run_count")]
        public int Job_Run_Count { get; set; }

        /// <summary>
        /// Use values from <see cref="SosyncState"/> only.
        /// </summary>
        [DataMember(Name = "job_state")]
        public string Job_State { get; set; }

        /// <summary>
        /// Use values from <see cref="SosyncError"/> only.
        /// </summary>
        [DataMember(Name = "job_error_code")]
        public string Job_Error_Code { get; set; }

        [DataMember(Name = "job_error_text")]
        public string Job_Error_Text { get; set; }

        [DataMember(Name = "job_log")]
        public string Job_Log { get; set; }

        // Parent job

        [DataMember(Name = "parent_job_id")]
        public int? Parent_Job_ID { get; set; }

        // Child jobs processing time

        [DataMember(Name = "child_job_start")]
        public DateTime? Child_Job_Start { get; set; }

        [DataMember(Name = "child_job_end")]
        public DateTime? Child_Job_End { get; set; }

        // Synchronization source

        /// <summary>
        /// Use values from <see cref="SosyncSystem"/> only.
        /// </summary>
        [DataMember(Name = "sync_source_system")]
        public string Sync_Source_System { get; set; }

        [DataMember(Name = "sync_source_model")]
        public string Sync_Source_Model { get; set; }

        [DataMember(Name = "sync_source_record_id")]
        public int? Sync_Source_Record_ID { get; set; }

        [DataMember(Name = "sync_source_merge_into_record_id")]
        public int? Sync_Source_Merge_Into_Record_ID { get; set; }

        // Synchronization target

        /// <summary>
        /// Use values from <see cref="SosyncSystem"/> only.
        /// </summary>
        [DataMember(Name = "sync_target_system")]
        public string Sync_Target_System { get; set; }

        [DataMember(Name = "sync_target_model")]
        public string Sync_Target_Model { get; set; }

        [DataMember(Name = "sync_target_record_id")]
        public int? Sync_Target_Record_ID { get; set; }

        [DataMember(Name = "sync_target_merge_into_record_id")]
        public int? Sync_Target_Merge_Into_Record_ID { get; set; }

        // Synchronization info

        [DataMember(Name = "sync_source_data")]
        public string Sync_Source_Data { get; set; }

        [DataMember(Name = "sync_target_data_before")]
        public string Sync_Target_Data_Before { get; set; }

        [DataMember(Name = "sync_target_request")]
        public string Sync_Target_Request { get; set; }

        [DataMember(Name = "sync_target_answer")]
        public string Sync_Target_Answer{ get; set; }

        // Synchronization processing time

        [DataMember(Name = "sync_start")]
        public DateTime? Sync_Start { get; set; }

        [DataMember(Name = "sync_end")]
        public DateTime? Sync_End { get; set; }
        #endregion
    }
}
