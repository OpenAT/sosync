using DaDi.Odoo;
using dadi_data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSosync.Data.Models;

namespace Syncer.Services
{
    /// <summary>
    /// Wraps the <see cref="OdooClient"/> class in a service to be
    /// consumed by dependency injection.
    /// </summary>
    public class OdooService
    {
        #region Members
        private OdooClient _client;
        private ILogger _log;
        #endregion

        #region Properties
        public OdooClient Client
        {
            get { return _client; }
        }
        #endregion

        #region Constructors
        public OdooService(SosyncOptions options, ILogger<OdooService> logger)
        {
            _log = logger;

            _client = new OdooClient($"http://{options.Online_Host}/xmlrpc/2/", options.Instance);
            _client.Authenticate(options.Online_Sosync_User, options.Online_Sosync_PW);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Synchronizes the specified job to FSO and updates the job_fso_id accordingly.
        /// </summary>
        public int? SendSyncJob(SyncJob job)
        {
            _log.LogDebug($"Updating job {job.Job_ID} in FSOnline");

            if (job.Job_Fso_ID.HasValue)
            {
                // If job_fso_id is already known, just update fso
                Client.UpdateModel<SyncJob>("sosync.job", job, job.Job_Fso_ID.Value);
                return null;
            }
            else
            {
                // Without the id, first check if fso already has the job_id.
                // Create it if it doesn't, else get it and update the id,
                // then update the job.
                // If more than one result is returend, SingleOrDefault throws
                // an exception
                int foundJobId = Client
                    .SearchModelByField<SyncJob, int>("sosync.job", x => x.Job_ID, job.Job_ID)
                    .FirstOrDefault();

                if (foundJobId == 0)
                {
                    // Job didn't exist yet, create it
                    int newId = Client.CreateModel<SyncJob>("sosync.job", job);
                    return newId;
                }
                else
                {
                    // Job was found, update its id in sync_table, then update it
                    Client.UpdateModel<SyncJob>("sosync.job", job, foundJobId);
                    return foundJobId;
                }
            }
        }
        #endregion
    }
}
