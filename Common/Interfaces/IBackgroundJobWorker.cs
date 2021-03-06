﻿using System;
using System.Threading;
using WebSosync.Common.Events;
using WebSosync.Data.Models;

namespace WebSosync.Common.Interfaces
{
    public interface IBackgroundJobWorker
    {
        void ConfigureCancellation(CancellationToken token);
        void Start();

        event EventHandler Cancelling;
        event EventHandler<RequireRestartEventArgs> RequireRestart;
    }
}
