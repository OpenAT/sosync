using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebSosync.Data.Models;
using WebSosync.Models;

namespace WebSosync.Extensions
{
    public static class JobDtoExtensions
    {
        public static IEnumerable<SosyncJobEntity> ToEntities(this JobDto job, SosyncJobEntity parent = null)
        {
            var result = new List<SosyncJobEntity>();

            var jobEntity = job.ToEntity();
            jobEntity.ParentJob = parent;
            
            result.Add(jobEntity);

            if (job.children != null)
            {
                var childEntities = job.children
                    .SelectMany(c => c.ToEntities(jobEntity));

                result.AddRange(childEntities);
            }

            return result;
        }

        private static SosyncJobEntity ToEntity(this JobDto job)
        {
            var result = new SosyncJobEntity()
            {
                JobDate = job.job_date,
                JobSourceSystem = job.job_source_system,
                JobSourceModel = job.job_source_model,
                JobSourceRecordId = job.job_source_record_id,
                JobSourceType = job.job_source_type,
                JobSourceMergeIntoRecordId = job.job_source_merge_into_record_id,
                JobSourceTargetMergeIntoRecordId = job.job_source_target_merge_into_record_id,
                JobSourceTargetRecordId = job.job_source_target_record_id,
                JobSourceSosyncWriteDate = job.job_source_sosync_write_date,
                JobSourceFields = JsonSerializer.Serialize(job.job_source_fields),
                JobPriority = job.job_priority,
            };
            return result;
        }
    }
}
