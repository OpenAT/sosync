using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebSosync.Interfaces;

namespace WebSosync
{
    public class HostService : IHostService
    {
        #region Constructors
        public HostService()
        {
            _tokenSource = new CancellationTokenSource();
            Token = _tokenSource.Token;
        }
        #endregion

        #region Methods
        public void RequestShutdown()
        {
            _tokenSource.Cancel();
        }
        #endregion

        #region Properties
        public CancellationToken Token { get; private set; }
        #endregion

        #region Members
        private CancellationTokenSource _tokenSource;
        #endregion
    }
}
