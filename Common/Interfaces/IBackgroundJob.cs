using WebSosync.Common.Enumerations;

namespace WebSosync.Common.Interfaces
{
    public interface IBackgroundJob<T> where T: IBackgroundJobWorker
    {
        BackgoundJobState Status { get; }

        bool ShutdownPending { get; set; }
        bool RestartOnFinish { get; set; }

        void Start();
        void Stop();
    }
}
