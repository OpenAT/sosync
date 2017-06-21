using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebSosync.Enumerations;

namespace WebSosync.Interfaces
{
    public interface IBackgroundJob
    {
        ServiceState Status { get; }

        bool ShutdownPending { get; set; }
        bool RestartOnFinish { get; set; }

        void Start();
        void Stop();
    }
}
