using Odoo;
using System.Xml;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Workers
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
            using (var db = new DataService(Configuration))
            {
                var jobs = db.GetJobs(false);

                var client = new OdooClient($"http://{Configuration.Online_Host}/xmlrpc/2/", Configuration.Instance);
                client.Authenticate(Configuration.Online_Sosync_User, Configuration.Online_Sosync_PW);

                //var job = client.GetModel<SyncJob>("sosync.job", 18);
                //int id = client.CreateModel<SyncJob>("sosync.job", jobs[0]);
                //jobs[0].Job_Fso_ID = id;
            }
        }
        #endregion
    }
}
