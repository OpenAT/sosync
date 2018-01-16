using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Data.Models;
using System.Diagnostics;

namespace Syncer.Flows
{
    /// <summary>
    /// Base class for sync flows that deal 
    /// </summary>
    public abstract class MergeSyncFlow : SyncFlow
    {
        public MergeSyncFlow(IServiceProvider svc) : base(svc)
        {
        }

        /// <summary>
        /// Starts the sync flow.
        /// </summary>
        /// <param name="flowService">The flow service for handling child jobs.</param>
        /// <param name="job">The job that initiated this sync flow.</param>
        /// <param name="loadTimeUTC">The loading time of the job.</param>
        /// <param name="requireRestart">Reference parameter to indicate that the syncer should restart immediately after this flow ends.</param>
        /// <param name="restartReason">Reference parameter to indicate the reason why the restart was requested.</param>
        protected override void StartFlow(FlowService flowService, DateTime loadTimeUTC, ref bool requireRestart, ref string restartReason)
        {
            UpdateJobRunCount(Job);

            CheckRunCount(5);

            DateTime? initialWriteDate = null;
            Stopwatch consistencyWatch = new Stopwatch();

            HandleChildJobs(flowService, initialWriteDate, consistencyWatch, ref requireRestart, ref restartReason);
            HandleTransformation(initialWriteDate, consistencyWatch, ref requireRestart, ref restartReason);
        }
    }
}
