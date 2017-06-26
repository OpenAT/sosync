using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSosync.Controllers;
using WebSosync.Interfaces;
using WebSosync.Enumerations;
using Syncer;
using Microsoft.Extensions.Configuration;
using WebSosync.Data.Models;
using Microsoft.Extensions.Options;

namespace WebSosync
{
    public class BackgroundJob : IBackgroundJob
    {
        #region Constructors
        /// <summary>
        /// Creates a new instance of the <see cref="BackgroundJob"/> class.
        /// </summary>
        /// <param name="logger">The logger used for logging.</param>
        public BackgroundJob(ILogger<BackgroundJob> logger, IOptions<SosyncOptions> configuration)
        {
            _log = logger;
            _lockObj = new object();
            _config = configuration.Value;

            Status = ServiceState.Stopped;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Start the background job.
        /// </summary>
        public void Start()
        {
            lock (_lockObj)
            {
                if (_task == null)
                {
                    _tokenSource = new CancellationTokenSource();
                    _token = _tokenSource.Token;

                    _task = new Task(DoWork, _token);
                }

                _task.ContinueWith(OnFinished);
                _task.Start();

                RestartOnFinish = false;
                Status = ServiceState.Running;
            }
        }

        /// <summary>
        /// Stops the background job. Pending restart requests will be removed.
        /// </summary>
        public void Stop()
        {
            lock (_lockObj)
            {
                // On explicit stop, ignore requested restarts
                RestartOnFinish = false;

                if (_tokenSource != null)
                    _tokenSource.Cancel();
            }
        }

        /// <summary>
        /// Does the heavy lifting.
        /// </summary>
        private void DoWork()
        {
            // If a cancellation is pending right from the start, exit immediately
            if (_token.IsCancellationRequested)
                return;

            SyncProcess syncer = new SyncProcess(_token, _config);
            syncer.Synchronize();

            // This loop simulates the sync process. Replace this with loading and
            // processing the jobs. Check cancellation token after each completed
            // job for graceful termination.

            for (int i = 0; i < 5400; i++)
            {
                _log.LogInformation($"JOB: running, i = {i}");

                System.Threading.Thread.Sleep(2000);

                if (_token.IsCancellationRequested)
                {
                    Status = ServiceState.Stopping;

                    // Clean up here, if necessary

                    _log.LogInformation("JOB: stopped gracefully.");
            return;
                }
            }
        }

        /// <summary>
        /// Runs, when the task completes, independent of the task result or state.
        /// </summary>
        /// <param name="previous">The previously ended task which is continued by this method.</param>
        private void OnFinished(Task previous)
        {
            try
            {
                lock (_lockObj)
                {
                    _tokenSource.Dispose();
                    _tokenSource = null;
                    _task = null;

                    // If the task had no exception, the finished state is "stopped", else it's error
                    Status = previous.Exception == null ? ServiceState.Stopped : ServiceState.Error;
                }

                // If a restart was requested, immediately start again
                if (RestartOnFinish)
                    Start();
            }
            catch (Exception ex)
            {
                // Log any exceptions that happened during the finish handler
                if (previous.Exception != null)
                    _log.LogError(ex.ToString());
            }
            finally
            {
                // Always log the task exception, if any
                if (previous.Exception != null)
                    _log.LogError(previous.Exception.ToString());
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// The current status of the background job.
        /// </summary>
        public ServiceState Status { get; private set; }

        /// <summary>
        /// Indicates a pending shutdown to the job, avoiding restarts.
        /// </summary>
        public bool ShutdownPending { get; set; }

        /// <summary>
        /// If set to true, the job will immediately restart after it finished.
        /// </summary>
        public bool RestartOnFinish
        {
            get { return _restartOnFinish; }
            set
            {
                lock (_lockObj)
                {
                    _restartOnFinish = value;

                    if (_restartOnFinish && Status == ServiceState.Running)
                        Status = ServiceState.RunningRestartRequested;
                }
            }
        }
        #endregion

        #region Members
        private object _lockObj;
        private Task _task;
        private CancellationTokenSource _tokenSource;
        private CancellationToken _token;
        private bool _restartOnFinish;
        private ILogger<BackgroundJob> _log;
        private SosyncOptions _config;
        #endregion
    }
}
