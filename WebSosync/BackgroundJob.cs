using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncer;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WebSosync.Common.Enumerations;
using WebSosync.Common.Events;
using WebSosync.Common.Interfaces;
using WebSosync.Data.Models;
using WebSosync.Enumerations;
using WebSosync.Interfaces;

namespace WebSosync
{
    public class BackgroundJob<T> : IBackgroundJob<T> where T: IBackgroundJobWorker
    {
        #region Members
        private object _lockObj;
        private Task _task;
        private CancellationTokenSource _tokenSource;
        private CancellationToken _token;
        private bool _restartOnFinish;
        private ILogger<BackgroundJob<T>> _log;
        private SosyncOptions _config;
        private IServiceProvider _svc;
        #endregion

        #region Properties
        /// <summary>
        /// The current status of the background job.
        /// </summary>
        public BackgoundJobState Status { get; private set; }

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

                    if (_restartOnFinish && Status == BackgoundJobState.Running)
                        Status = BackgoundJobState.RunningRestartRequested;
                }
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new instance of the <see cref="BackgroundJob"/> class.
        /// </summary>
        /// <param name="logger">The logger used for logging.</param>
        public BackgroundJob(IServiceProvider svc, ILogger<BackgroundJob<T>> logger, SosyncOptions config)
        {
            _svc = svc;
            _log = logger;
            _lockObj = new object();
            _config = config;

            Status = BackgoundJobState.Idle;
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
                    _task.ContinueWith(OnFinished);
                    _task.Start();

                    RestartOnFinish = false;
                    Status = BackgoundJobState.Running;
                }
                else
                {
                    if (!ShutdownPending)
                    {
                        RestartOnFinish = true;
                        Status = BackgoundJobState.RunningRestartRequested;
                    }
                }
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

            try
            {
                _log.LogInformation($"BackgroundJob-{typeof(T).Name}: starting process");

                Stopwatch s = new Stopwatch();
                s.Start();

                T worker = _svc.GetService<T>();
                worker.ConfigureCancellation(_token);
                worker.Cancelling += Syncer_Cancelling;
                worker.RequireRestart += Syncer_RequireRestart;

                worker.Start();

                worker.RequireRestart -= Syncer_RequireRestart;
                worker.Cancelling -= Syncer_Cancelling;

                s.Stop();

                _log.LogInformation($"BackgroundJob-{typeof(T).Name}: finished in {s.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                _log.LogError($"BackgroundJob-{typeof(T).Name}: {ex.ToString()}");
            }
        }

        private void Syncer_Cancelling(object sender, EventArgs e)
        {
            Status = BackgoundJobState.Stopping;
            _log.LogInformation($"BackgroundJob-{typeof(T).Name}: cancelling gracefully");
        }

        private void Syncer_RequireRestart(object sender, RequireRestartEventArgs e)
        {
            RestartOnFinish = true;
            _log.LogInformation($"BackgroundJob-{typeof(T).Name}: requested restart, reason: {e.Reason}");
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
                    Status = previous.Exception == null ? BackgoundJobState.Idle : BackgoundJobState.Error;
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
    }
}
