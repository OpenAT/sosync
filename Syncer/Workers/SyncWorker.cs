using Microsoft.Extensions.DependencyInjection;
using Syncer.Processors;
using Syncer.Services;
using System;
using System.Linq;
using System.Threading;
using WebSosync.Data;
using WebSosync.Data.Extensions;
using WebSosync.Data.Models;

namespace Syncer.Workers
{
    /// <summary>
    /// The sync worker represents the background thread, loading and processing jobs.
    /// </summary>
    public class SyncWorker : WorkerBase
    {
        #region Members
        private IServiceProvider _svc;
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new instance of the <see cref="SyncWorker"/> class.
        /// </summary>
        /// <param name="options">Options to be used for data connections etc.</param>
        public SyncWorker(IServiceProvider svc, SosyncOptions options)
            : base(options)
        {
            _svc = svc;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Starts the syncer.
        /// </summary>
        public override void Start()
        {
            // Get only the first open job and its hierarchy,
            // and build the tree in memory
            var job = GetNextOpenJob();

            while (job != null)
            {
                var processor = _svc.GetService<JobProcessor>();
                processor.Process(job);

                // Stop processing the queue if cancellation was requested
                if (CancellationToken.IsCancellationRequested)
                {
                    // Raise the cancelling event
                    RaiseCancelling();
                    // Clean up here, if necessary
                }

                job = GetNextOpenJob();
            }
        }

        private SyncJob GetNextOpenJob()
        {
            using (var db = _svc.GetService<DataService>())
            {
                var result = db.GetFirstOpenJobHierarchy().ToTree(
                        x => x.Job_ID,
                        x => x.Parent_Job_ID,
                        x => x.Children)
                        .SingleOrDefault();

                return result;
            }
        }
        #endregion
    }
}
