using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using WebSosync.Models;

namespace WebSosync.Services
{
    public class JobDtoValidator
    {
        public Dictionary<string, string> ValidateCreation(SyncJobDto job)
        {
            var result = new Dictionary<string, string>();

            if (job.SourceSystem == "")
            {
                var serializedName = typeof(SyncJobDto).GetProperty(
                    nameof(job.SourceSystem)
                    ).GetCustomAttribute<DataMemberAttribute>().Name;
                result.Add(serializedName, $"{serializedName} cannot be empty.");
            }

            if (job.SourceModel == "")
            {
                var serializedName = typeof(SyncJobDto).GetProperty(
                    nameof(job.SourceModel)
                    ).GetCustomAttribute<DataMemberAttribute>().Name;
                result.Add(serializedName, $"{serializedName} cannot be empty.");
            }

            if (job.SourceRecordID == null)
            {
                var serializedName = typeof(SyncJobDto).GetProperty(
                    nameof(job.SourceRecordID)
                    ).GetCustomAttribute<DataMemberAttribute>().Name;
                result.Add(serializedName, $"{serializedName} is required.");
            }
            else if (job.SourceRecordID == 0)
            {
                var serializedName = typeof(SyncJobDto).GetProperty(
                    nameof(job.SourceRecordID)
                    ).GetCustomAttribute<DataMemberAttribute>().Name;
                result.Add(serializedName, $"{serializedName} cannot be zero (0).");
            }

            return result;  
        }
    }
}
