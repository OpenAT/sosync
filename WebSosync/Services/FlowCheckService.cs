using dadi_data.Models;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebSosync.Data;
using WebSosync.Data.Models;
using WebSosync.Models;

namespace WebSosync.Services
{
    public class FlowCheckService
    {
        private FlowService _flowService;
        private DataService _db;
        private MdbService _mdb;
        private OdooService _odoo;

        public FlowCheckService(
            FlowService flowService,
            DataService db,
            MdbService mdb,
            OdooService odoo)
        {
            _flowService = flowService;
            _db = db;
            _mdb = mdb;
            _odoo = odoo;
        }

        public async Task<SyncModelState> GetModelState(
            string modelName,
            int id,
            int? foreignId)
        {
            var flowInfo = _flowService
                .GetFlowInfo(modelName);

            var alternateModelName = "";
            int? onlineID = null;
            int? studioID = null;

            if (flowInfo.OnlineModelName == modelName)
            {
                alternateModelName = flowInfo.StudioModelName;
                studioID = id;
                onlineID = foreignId;
            }
            else
            {
                alternateModelName = flowInfo.StudioModelName;
                onlineID = id;
                studioID = foreignId;
            }

            if (string.IsNullOrEmpty(alternateModelName))
                throw new Exception("Model not found");

            var jobErrors = await GetJobErrorsAsync(
                modelName, 
                alternateModelName, 
                id, 
                foreignId);

            var inSync = false;
            var info = "";

            if (jobErrors.Error + jobErrors.ErrorRetry == 0)
            {
                inSync = await IsModelSynchronized(flowInfo,
                    onlineID,
                    studioID);

                info = inSync ? "" : "Sosync write dates are different";
            }
            else
            {
                info = "Error jobs found";
            }

            return new SyncModelState()
            {
                InSync = inSync,
                JobErrors = jobErrors.Error,
                JobErrorsRetry = jobErrors.ErrorRetry,
                Information = info
            };
        }

        public async Task<JobErrorCount> GetJobErrorsAsync(
            string modelName,
            string alternateModelName,
            int id,
            int? foreignId)
        {

            var query1 = _db.GetModelErrorCountAsync(modelName, id);
            Task<JobErrorCount> query2 = null;

            if (foreignId != null)
                query2 = _db.GetModelErrorCountAsync(alternateModelName, foreignId.Value);

            var result = new JobErrorCount();

            var result1 = await query1;
            result.Error += result1.Error;
            result.ErrorRetry += result1.ErrorRetry;

            if (foreignId != null)
            {
                var result2 = await query2;
                result.Error += result2.Error;
                result.ErrorRetry += result2.ErrorRetry;
            }

            return result;
        }

        public async Task<bool> IsModelSynchronized(
            FlowInfo flowInfo,
            int? onlineID,
            int? studioID)
        {
            using (var db = _mdb.GetDataService<dboTypen>())
            {
                var studioTime = await db.ExecuteQueryAsync<DateTime>("");
            }

            return true;
        }
    }
}
