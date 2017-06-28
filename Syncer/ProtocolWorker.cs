using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WebSosync.Common.Interfaces;
using WebSosync.Data.Models;

namespace Syncer
{
    public class ProtocolWorker : WorkerBase
    {
        #region Constructors
        public ProtocolWorker(SosyncOptions options)
            : base(options)
        { }
        #endregion

        #region Methods
        public override void Start()
        {
            System.Threading.Thread.Sleep(10000);
        }
        #endregion
    }
}
