using System;
using System.Threading;
using WebSosync.Data.Models;

namespace WebSosync.Common.Interfaces
{
    public interface IBackgroundJobWorker
    {
        string Name { get; }

        void ConfigureCancellation(CancellationToken token);
        void Start();

        event EventHandler Cancelling;
    }
}
