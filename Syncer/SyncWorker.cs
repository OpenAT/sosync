using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WebSosync.Common.Interfaces;
using WebSosync.Data;
using WebSosync.Data.Extensions;
using WebSosync.Data.Helpers;
using WebSosync.Data.Models;

namespace Syncer
{
    public class SyncWorker : IBackgroundJobWorker
    {
        #region IBackgroundJobWorker implementation
        public string Name { get; private set; }
        public event EventHandler Cancelling;

        /// <summary>
        /// Sets the specified cancellation token for this worker.
        /// </summary>
        /// <param name="token">The token to be used to check for cancellation</param>
        public void ConfigureCancellation(CancellationToken token)
        {
            _cancelToken = token;
        }
        #endregion

        #region Members
        private CancellationToken _cancelToken;
        private SosyncOptions _config;
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new instance of the <see cref="SyncWorker"/> class.
        /// </summary>
        /// <param name="options">Options to be used for data connections etc.</param>
        public SyncWorker(SosyncOptions options)
        {
            _config = options;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Starts the syncer.
        /// </summary>
        public void Start()
        {
#warning TODO: At some point we want job priority.
            // When that happens, change the datalayer to return a single job hierarchy,
            // then process that one hierarchy, rince and repeat until there is no open
            // job.

            IList<SyncJob> jobs = null;
            using (var db = new DataService(_config))
            {
                // Get all open jobs and build the job tree in memory
                jobs = db.GetJobs().ToTree(
                    x => x.Job_ID,
                    x => x.Parent_Job_ID,
                    x => x.Children);
            }

            // Now process all root jobs
            foreach (var job in jobs)
            {
                // Process job here
                System.Threading.Thread.Sleep(100);

                // Stop processing the queue if cancellation was requested
                if (_cancelToken.IsCancellationRequested)
                {
                    // Raise the cancelling event
                    Cancelling?.Invoke(this, EventArgs.Empty);
                    // Clean up here, if necessary
                }
            }
        }
        #endregion
    }
}
