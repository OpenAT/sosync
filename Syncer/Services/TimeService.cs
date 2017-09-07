using System;
using Yort.Ntp;

namespace Syncer.Services
{
    public class TimeService
    {
        #region Constructors
        public TimeService()
        {
            _threadLock = new object();
            _ntp = new NtpClient("at.pool.ntp.org");
        }
        #endregion

        #region Methods
        public void ThrowOnMismatch()
        {
            var x = 0d;
            lock (_threadLock)
            {
                var internetTime = _ntp.RequestTimeAsync().Result;
                var offset = DateTime.UtcNow - internetTime;

                x = offset.TotalMilliseconds;
            }
        }
        #endregion

        #region Members
        private NtpClient _ntp;
        private object _threadLock;
        #endregion
    }
}
