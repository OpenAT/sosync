using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WebSosync.Common.Interfaces;
using WebSosync.Data;
using WebSosync.Data.Extensions;
using WebSosync.Data.Helpers;
using WebSosync.Data.Models;

namespace Syncer
{
    /// <summary>
    /// The sync worker represents the background thread, loading and processing jobs.
    /// </summary>
    public class SyncWorker : WorkerBase
    {
        #region Constructors
        /// <summary>
        /// Creates a new instance of the <see cref="SyncWorker"/> class.
        /// </summary>
        /// <param name="options">Options to be used for data connections etc.</param>
        public SyncWorker(SosyncOptions options)
            : base(options)
        { }
        #endregion

        #region Methods
        /// <summary>
        /// Starts the syncer.
        /// </summary>
        public override void Start()
        {
#warning TODO: At some point we want job priority.
            // When that happens, change the datalayer to return a single job hierarchy,
            // then process that one hierarchy, rince and repeat until there is no open
            // job.

            using (var db = new DataService(Configuration))
            {
                // Get the first open job and its hierarchy,
                // and build the tree in memory
                var job = GetNextOpenJob(db);

                while (job != null)
                {

                    // Stop processing the queue if cancellation was requested
                    if (CancellationToken.IsCancellationRequested)
                    {
                        // Raise the cancelling event
                        RaiseCancelling();
                        // Clean up here, if necessary
                    }

                    job = GetNextOpenJob(db);
                }
            }
        }

        private SyncJob GetNextOpenJob(DataService db)
        {
            var result = db.GetFirstOpenJobHierarchy().ToTree(
                    x => x.Job_ID,
                    x => x.Parent_Job_ID,
                    x => x.Children)
                    .SingleOrDefault();

            return result;
        }
        #endregion
    }
}
