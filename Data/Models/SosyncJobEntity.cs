using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data.Interfaces;

namespace WebSosync.Data.Models
{
    public class SosyncJobEntity
        : IAuditable
    {
        public SosyncJobEntity()
        {
            InverseJobClosedByJob = new HashSet<SosyncJobEntity>();
            InverseParentJob = new HashSet<SosyncJobEntity>();
        }

        public int Id { get; set; }
        public DateTime? JobDate { get; set; }
        public DateTime? JobFetched { get; set; }
        public int? JobPriority { get; set; }
        public string JobSourceSystem { get; set; }
        public string JobSourceModel { get; set; }
        public int? JobSourceRecordId { get; set; }
        public int? JobSourceTargetRecordId { get; set; }
        public DateTime? JobSourceSosyncWriteDate { get; set; }
        public string JobSourceFields { get; set; }
        public string JobSourceType { get; set; }
        public string JobSourceTypeInfo { get; set; }
        public int? JobSourceMergeIntoRecordId { get; set; }
        public int? JobSourceTargetMergeIntoRecordId { get; set; }
        public DateTime? JobStart { get; set; }
        public DateTime? JobEnd { get; set; }
        public int? JobDuration { get; set; }
        public int? JobRunCount { get; set; }
        public int? JobClosedByJobId { get; set; }
        public string JobLog { get; set; }
        public string JobState { get; set; }
        public string JobErrorCode { get; set; }
        public string JobErrorText { get; set; }
        public int? ParentJobId { get; set; }
        public string ParentPath { get; set; }
        public DateTime? ChildJobStart { get; set; }
        public DateTime? ChildJobEnd { get; set; }
        public int? ChildJobDuration { get; set; }
        public string SyncSourceSystem { get; set; }
        public string SyncSourceModel { get; set; }
        public int? SyncSourceRecordId { get; set; }
        public int? SyncSourceMergeIntoRecordId { get; set; }
        public string SyncTargetSystem { get; set; }
        public string SyncTargetModel { get; set; }
        public int? SyncTargetRecordId { get; set; }
        public int? SyncTargetMergeIntoRecordId { get; set; }
        public string SyncSourceData { get; set; }
        public string SyncTargetDataBefore { get; set; }
        public string SyncTargetDataAfter { get; set; }
        public string SyncTargetRequest { get; set; }
        public string SyncTargetAnswer { get; set; }
        public DateTime? SyncStart { get; set; }
        public DateTime? SyncEnd { get; set; }
        public int? SyncDuration { get; set; }
        public int? CreateUid { get; set; }
        public DateTime? CreateDate { get; set; }
        public int? WriteUid { get; set; }
        public DateTime? WriteDate { get; set; }

        public virtual SosyncJobEntity JobClosedByJob { get; set; }
        public virtual SosyncJobEntity ParentJob { get; set; }
        public virtual ICollection<SosyncJobEntity> InverseJobClosedByJob { get; set; }
        public virtual ICollection<SosyncJobEntity> InverseParentJob { get; set; }
    }
}
