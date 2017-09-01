using System;
using System.Threading;
using WebSosync.Common.Events;
using WebSosync.Common.Interfaces;
using WebSosync.Data.Models;

namespace Syncer.Workers
{
    public abstract class WorkerBase : IBackgroundJobWorker
    {
        #region IBackgroundJobWorker implementation
        public event EventHandler Cancelling;
        public event EventHandler<RequireRestartEventArgs> RequireRestart;

        public void ConfigureCancellation(CancellationToken token)
        {
            CancellationToken = token;
        }

        public abstract void Start();
        #endregion

        #region Properties
        protected CancellationToken CancellationToken { get; private set; }
        protected SosyncOptions Configuration { get; private set; }
        #endregion

        #region Constructors
        public WorkerBase(SosyncOptions options)
        {
            Configuration = options;
        }
        #endregion

        #region Methods
        protected void RaiseCancelling()
        {
            Cancelling?.Invoke(this, EventArgs.Empty);
        }

        protected void RaiseRequireRestart(string reason)
        {
            RequireRestart?.Invoke(this, new RequireRestartEventArgs(reason));
        }
        #endregion
    }
}
