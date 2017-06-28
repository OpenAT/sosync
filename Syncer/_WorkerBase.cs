using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WebSosync.Common.Interfaces;
using WebSosync.Data.Models;

namespace Syncer
{
    public abstract class WorkerBase : IBackgroundJobWorker
    {
        #region IBackgroundJobWorker implementation
        public event EventHandler Cancelling;

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
        #endregion
    }
}
