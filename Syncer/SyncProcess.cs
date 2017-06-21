using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WebSosync.Data;
using WebSosync.Data.Extensions;
using WebSosync.Data.Helpers;

namespace Syncer
{
    public class SyncProcess
    {
        #region Constructors
        public SyncProcess(CancellationToken cancelToken, IConfiguration configuration)
        {
            _cancelToken = cancelToken;
            _config = configuration;
        }
        #endregion

        #region Methods
        public void Synchronize()
        {
            using (var db = new DataService(ConnectionHelper.GetPostgresConnectionString(
                _config["instance"],
                _config["sosync_user"],
                _config["sosync_pass"])))
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
        private IConfiguration _config;
        #endregion
    }
}
