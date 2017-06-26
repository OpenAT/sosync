using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WebSosync.Data;
using WebSosync.Data.Extensions;
using WebSosync.Data.Helpers;
using WebSosync.Data.Models;

namespace Syncer
{
    public class SyncProcess
    {
        #region Constructors
        public SyncProcess(CancellationToken cancelToken, SosyncConfiguration configuration)
        {
            _cancelToken = cancelToken;
            _config = configuration;
        }
        #endregion

        #region Methods
        public void Synchronize()
        {
            using (var db = new DataService(_config))
            {
                var jobs = db.GetSyncJobs().ToTree();

                foreach (var job in jobs)
                {

                }
            }
        }
        #endregion

        #region Members
        private CancellationToken _cancelToken;
        private SosyncConfiguration _config;
        #endregion
    }
}
