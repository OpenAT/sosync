using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSosync.Data.Interfaces;
using WebSosync.Data.Models;

namespace WebSosync.Data
{
    public class SosyncGuiContext
        : DbContext
    {
        public DbSet<SosyncJobEntity> SosyncJobs { get; set; }

        public SosyncGuiContext()
            : base()
        { }

        public SosyncGuiContext(DbContextOptions<SosyncGuiContext> options)
            : base(options)
        { }

        private void AuditEntities()
        {
            var models = ChangeTracker.Entries<IAuditable>();

            foreach (var model in models)
            {
                switch (model.State)
                {
                    case EntityState.Added:
                        model.Entity.CreateDate = DateTime.UtcNow;
                        break;

                    case EntityState.Modified:
                        model.Entity.WriteDate = DateTime.UtcNow;
                        break;
                }
            }
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            AuditEntities();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            AuditEntities();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("ProductVersion", "2.2.6-servicing-10079");

            modelBuilder.Entity<SosyncJobEntity>(entity =>
            {
                entity.ToTable("sosync_job");

                entity.ForNpgsqlHasComment("sosync.job");

                entity.HasIndex(e => e.JobClosedByJobId)
                    .HasName("sosync_job_job_closed_by_job_id_index");

                entity.HasIndex(e => e.JobSourceModel)
                    .HasName("sosync_job_job_source_model_index");

                entity.HasIndex(e => e.JobSourceSystem)
                    .HasName("sosync_job_job_source_system_index");

                entity.HasIndex(e => e.JobSourceType)
                    .HasName("sosync_job_job_source_type_index");

                entity.HasIndex(e => e.ParentJobId)
                    .HasName("sosync_job_parent_job_id_index");

                entity.HasIndex(e => e.SyncSourceRecordId)
                    .HasName("sosync_job_sync_source_record_id_index");

                entity.HasIndex(e => e.SyncTargetRecordId)
                    .HasName("sosync_job_sync_target_record_id_index");

                entity.HasIndex(e => new { e.JobState, e.JobPriority, e.JobDate })
                    .HasName("idx_job_sort_order");

                entity.HasIndex(e => new { e.JobPriority, e.JobDate, e.JobState, e.ParentJobId })
                    .HasName("get_first_open_jobs_v2_idx")
                    .HasFilter("(((job_state)::text = 'new'::text) AND (parent_job_id IS NULL))");

                entity.HasIndex(e => new { e.JobSourceSosyncWriteDate, e.JobSourceSystem, e.JobSourceModel, e.JobSourceRecordId, e.JobState })
                    .HasName("skip_jobs_idx")
                    .HasFilter("((job_state)::text = 'new'::text)");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.ChildJobDuration)
                    .HasColumnName("child_job_duration")
                    .ForNpgsqlHasComment("CJ Duration (ms)");

                entity.Property(e => e.ChildJobEnd)
                    .HasColumnName("child_job_end")
                    .ForNpgsqlHasComment("CJ Finished at");

                entity.Property(e => e.ChildJobStart)
                    .HasColumnName("child_job_start")
                    .ForNpgsqlHasComment("CJ Started at");

                entity.Property(e => e.CreateDate)
                    .HasColumnName("create_date")
                    .ForNpgsqlHasComment("Created on");

                entity.Property(e => e.CreateUid)
                    .HasColumnName("create_uid")
                    .ForNpgsqlHasComment("Created by");

                entity.Property(e => e.JobClosedByJobId)
                    .HasColumnName("job_closed_by_job_id")
                    .ForNpgsqlHasComment("Closed by Job");

                entity.Property(e => e.JobDate)
                    .HasColumnName("job_date")
                    .ForNpgsqlHasComment("Job Create Date");

                entity.Property(e => e.JobDuration)
                    .HasColumnName("job_duration")
                    .ForNpgsqlHasComment("Duration (ms)");

                entity.Property(e => e.JobEnd)
                    .HasColumnName("job_end")
                    .ForNpgsqlHasComment("Finished at");

                entity.Property(e => e.JobErrorCode)
                    .HasColumnName("job_error_code")
                    .HasColumnType("character varying")
                    .ForNpgsqlHasComment("Error Code");

                entity.Property(e => e.JobErrorText)
                    .HasColumnName("job_error_text")
                    .ForNpgsqlHasComment("Error Info");

                entity.Property(e => e.JobFetched)
                    .HasColumnName("job_fetched")
                    .ForNpgsqlHasComment("Fetched at");

                entity.Property(e => e.JobLog)
                    .HasColumnName("job_log")
                    .ForNpgsqlHasComment("Log");

                entity.Property(e => e.JobPriority)
                    .HasColumnName("job_priority")
                    .ForNpgsqlHasComment("Priority");

                entity.Property(e => e.JobRunCount)
                    .HasColumnName("job_run_count")
                    .ForNpgsqlHasComment("Run Count");

                entity.Property(e => e.JobSourceFields)
                    .HasColumnName("job_source_fields")
                    .ForNpgsqlHasComment("Fields");

                entity.Property(e => e.JobSourceMergeIntoRecordId)
                    .HasColumnName("job_source_merge_into_record_id")
                    .ForNpgsqlHasComment("MergeInto ID");

                entity.Property(e => e.JobSourceModel)
                    .HasColumnName("job_source_model")
                    .HasColumnType("character varying")
                    .ForNpgsqlHasComment("Model");

                entity.Property(e => e.JobSourceRecordId)
                    .HasColumnName("job_source_record_id")
                    .ForNpgsqlHasComment("Record ID");

                entity.Property(e => e.JobSourceSosyncWriteDate)
                    .HasColumnName("job_source_sosync_write_date")
                    .ForNpgsqlHasComment("sosync_write_date");

                entity.Property(e => e.JobSourceSystem)
                    .HasColumnName("job_source_system")
                    .HasColumnType("character varying")
                    .ForNpgsqlHasComment("System");

                entity.Property(e => e.JobSourceTargetMergeIntoRecordId)
                    .HasColumnName("job_source_target_merge_into_record_id")
                    .ForNpgsqlHasComment("MergeInto Trg. ID");

                entity.Property(e => e.JobSourceTargetRecordId)
                    .HasColumnName("job_source_target_record_id")
                    .ForNpgsqlHasComment("Target Rec. ID");

                entity.Property(e => e.JobSourceType)
                    .HasColumnName("job_source_type")
                    .HasColumnType("character varying")
                    .ForNpgsqlHasComment("Type");

                entity.Property(e => e.JobSourceTypeInfo)
                    .HasColumnName("job_source_type_info")
                    .HasColumnType("character varying")
                    .ForNpgsqlHasComment("Syncflow Indi.");

                entity.Property(e => e.JobStart)
                    .HasColumnName("job_start")
                    .ForNpgsqlHasComment("Started at");

                entity.Property(e => e.JobState)
                    .HasColumnName("job_state")
                    .HasColumnType("character varying")
                    .ForNpgsqlHasComment("State");

                entity.Property(e => e.ParentJobId)
                    .HasColumnName("parent_job_id")
                    .ForNpgsqlHasComment("Parent Job");

                entity.Property(e => e.ParentPath)
                    .HasColumnName("parent_path")
                    .HasColumnType("character varying")
                    .ForNpgsqlHasComment("Path");

                entity.Property(e => e.SyncDuration)
                    .HasColumnName("sync_duration")
                    .ForNpgsqlHasComment("Sync Duration (ms)");

                entity.Property(e => e.SyncEnd)
                    .HasColumnName("sync_end")
                    .ForNpgsqlHasComment("Sync End");

                entity.Property(e => e.SyncSourceData)
                    .HasColumnName("sync_source_data")
                    .ForNpgsqlHasComment("Sync Source Data");

                entity.Property(e => e.SyncSourceMergeIntoRecordId)
                    .HasColumnName("sync_source_merge_into_record_id")
                    .ForNpgsqlHasComment("Source Merge-Into Record ID");

                entity.Property(e => e.SyncSourceModel)
                    .HasColumnName("sync_source_model")
                    .HasColumnType("character varying")
                    .ForNpgsqlHasComment("Source Model");

                entity.Property(e => e.SyncSourceRecordId)
                    .HasColumnName("sync_source_record_id")
                    .ForNpgsqlHasComment("Source Record ID");

                entity.Property(e => e.SyncSourceSystem)
                    .HasColumnName("sync_source_system")
                    .HasColumnType("character varying")
                    .ForNpgsqlHasComment("Source System");

                entity.Property(e => e.SyncStart)
                    .HasColumnName("sync_start")
                    .ForNpgsqlHasComment("Sync Start");

                entity.Property(e => e.SyncTargetAnswer)
                    .HasColumnName("sync_target_answer")
                    .ForNpgsqlHasComment("Sync Target Answer(s)");

                entity.Property(e => e.SyncTargetDataAfter)
                    .HasColumnName("sync_target_data_after")
                    .ForNpgsqlHasComment("Sync Target Data after");

                entity.Property(e => e.SyncTargetDataBefore)
                    .HasColumnName("sync_target_data_before")
                    .ForNpgsqlHasComment("Sync Target Data before");

                entity.Property(e => e.SyncTargetMergeIntoRecordId)
                    .HasColumnName("sync_target_merge_into_record_id")
                    .ForNpgsqlHasComment("Target Merge-Into Record ID");

                entity.Property(e => e.SyncTargetModel)
                    .HasColumnName("sync_target_model")
                    .HasColumnType("character varying")
                    .ForNpgsqlHasComment("Target Model");

                entity.Property(e => e.SyncTargetRecordId)
                    .HasColumnName("sync_target_record_id")
                    .ForNpgsqlHasComment("Target Record ID");

                entity.Property(e => e.SyncTargetRequest)
                    .HasColumnName("sync_target_request")
                    .ForNpgsqlHasComment("Sync Target Request(s)");

                entity.Property(e => e.SyncTargetSystem)
                    .HasColumnName("sync_target_system")
                    .HasColumnType("character varying")
                    .ForNpgsqlHasComment("Target System");

                entity.Property(e => e.WriteDate)
                    .HasColumnName("write_date")
                    .ForNpgsqlHasComment("Last Updated on");

                entity.Property(e => e.WriteUid)
                    .HasColumnName("write_uid")
                    .ForNpgsqlHasComment("Last Updated by");

                entity.HasOne(d => d.JobClosedByJob)
                    .WithMany(p => p.InverseJobClosedByJob)
                    .HasForeignKey(d => d.JobClosedByJobId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("sosync_job_job_closed_by_job_id_fkey");

                entity.HasOne(d => d.ParentJob)
                    .WithMany(p => p.InverseParentJob)
                    .HasForeignKey(d => d.ParentJobId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("sosync_job_parent_job_id_fkey");
            });

            //modelBuilder.Entity<SyncJob>(j =>
            //{
            //    j.ToTable("sosync_job", "public");
            //    j.Property(p => p.ID).HasColumnName("id");
            //    j.Property(p => p.Job_Closed_By_Job_ID).HasColumnName("job_closed_by_job_id");
            //    j.Property(p => p.Job_Date).HasColumnName("job_date");
            //    j.Property(p => p.Job_Priority).HasColumnName("job_priority");
            //    j.Property(p => p.Job_Source_Type).HasColumnName("job_source_type");
            //    j.Property(p => p.Job_Source_Type_Info).HasColumnName("job_source_type_info");
            //    j.Property(p => p.Job_Source_Merge_Into_Record_ID).HasColumnName("job_source_merge_into_record_id");
            //    j.Property(p => p.Job_Source_Target_Merge_Into_Record_ID).HasColumnName("job_source_target_merge_into_record_id");
            //    j.Property(p => p.Job_Source_System).HasColumnName("job_source_system");
            //    j.Property(p => p.Job_Source_Model).HasColumnName("job_source_model");
            //    j.Property(p => p.Job_Source_Record_ID).HasColumnName("job_source_record_id");
            //    j.Property(p => p.Job_Source_Target_Record_ID).HasColumnName("job_source_target_record_id");
            //    j.Property(p => p.Job_Source_Sosync_Write_Date).HasColumnName("job_source_sosync_write_date");
            //    j.Property(p => p.Job_Source_Fields).HasColumnName("job_source_fields");
            //    j.Property(p => p.Job_Fetched).HasColumnName("job_fetched");
            //    j.Property(p => p.Job_Start).HasColumnName("job_start");
            //    j.Property(p => p.Job_End).HasColumnName("job_end");
            //    j.Property(p => p.Job_Run_Count).HasColumnName("job_run_count");
            //    j.Property(p => p.Job_State).HasColumnName("job_state");
            //    j.Property(p => p.Job_Error_Code).HasColumnName("job_error_code");
            //    j.Property(p => p.Job_Error_Text).HasColumnName("job_error_text");
            //    j.Property(p => p.Job_Log).HasColumnName("job_log");
            //    j.Property(p => p.Parent_Job_ID).HasColumnName("parent_job_id");
            //    j.Property(p => p.Child_Job_Start).HasColumnName("child_job_start");
            //    j.Property(p => p.Child_Job_End).HasColumnName("child_job_end");
            //    j.Property(p => p.Sync_Source_System).HasColumnName("sync_source_system");
            //    j.Property(p => p.Sync_Source_Model).HasColumnName("sync_source_model");
            //    j.Property(p => p.Sync_Source_Record_ID).HasColumnName("sync_source_record_id");
            //    j.Property(p => p.Sync_Source_Merge_Into_Record_ID).HasColumnName("sync_source_merge_into_record_id");
            //    j.Property(p => p.Sync_Target_System).HasColumnName("sync_target_system");
            //    j.Property(p => p.Sync_Target_Model).HasColumnName("sync_target_model");
            //    j.Property(p => p.Sync_Target_Record_ID).HasColumnName("sync_target_record_id");
            //    j.Property(p => p.Sync_Target_Merge_Into_Record_ID).HasColumnName("sync_target_merge_into_record_id");
            //    j.Property(p => p.Sync_Source_Data).HasColumnName("sync_source_data");
            //    j.Property(p => p.Sync_Target_Data_Before).HasColumnName("sync_target_data_before");
            //    j.Property(p => p.Sync_Target_Request).HasColumnName("sync_target_request");
            //    j.Property(p => p.Sync_Target_Answer).HasColumnName("sync_target_answer");
            //    j.Property(p => p.Sync_Start).HasColumnName("sync_start");
            //    j.Property(p => p.Sync_End).HasColumnName("sync_end");
            //    j.Property(p => p.Write_Date).HasColumnName("write_date");
            //    j.Property(p => p.Create_Date).HasColumnName("create_date");
            //});
        }
    }
}
