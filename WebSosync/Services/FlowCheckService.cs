using DaDi.Odoo;
using DaDi.Odoo.Models;
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
                onlineID = id;
                studioID = foreignId;
            }
            else
            {
                alternateModelName = flowInfo.OnlineModelName;
                onlineID = foreignId;
                studioID = id;
            }

            if (string.IsNullOrEmpty(alternateModelName))
                throw new Exception("Model not found");

            var hasOpenJobs = await GetOpenJobAsync(
                modelName, 
                alternateModelName, 
                id, 
                foreignId);

            var inSync = false;
            var info = "";

            if (hasOpenJobs ?? false == false)
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
                HasOpenJobs = hasOpenJobs ?? false,
                Information = info
            };
        }

        public async Task<bool?> GetOpenJobAsync(
            string modelName,
            string alternateModelName,
            int id,
            int? foreignId)
        {

            var query1 = _db.GetModelErrorCountAsync(modelName, id);
            Task<bool?> query2 = null;

            if (foreignId != null)
                query2 = _db.GetModelErrorCountAsync(alternateModelName, foreignId.Value);

            bool? result = null;

            var result1 = await query1;

            if (foreignId != null)
            {
                var result2 = await query2;
            }

            return result;
        }

        public async Task<bool> IsModelSynchronized(
            FlowInfo flowInfo,
            int? onlineID,
            int? studioID)
        {
            DateTime? studioSosyncWriteDate = null;
            DateTime? onlineSosyncWriteDate = null;
            
            // Studio
            if (studioID != null)
                studioSosyncWriteDate = await GetStudioSosyncWriteDateViaStudioID(
                    flowInfo.StudioModelName,
                    studioID.Value);
            else
                studioSosyncWriteDate = await GetStudioSosyncWriteDateViaOnlineID(
                    flowInfo.StudioModelName,
                    onlineID.Value);

            // Online
            if (onlineID != null)
                onlineSosyncWriteDate = GetOnlineSosyncWriteDateViaOnlineID(
                    flowInfo.OnlineModelName,
                    onlineID.Value);
            else
                onlineSosyncWriteDate = GetOnlineSosyncWriteDateViaStudioID(
                    flowInfo.OnlineModelName,
                    studioID.Value);

            // Compare
            var hasBothDates = studioSosyncWriteDate != null
                && onlineSosyncWriteDate != null;

            return hasBothDates && studioSosyncWriteDate == onlineSosyncWriteDate;
        }

        private async Task<DateTime?> GetStudioSosyncWriteDateViaStudioID(
            string studioModelName,
            int studioID)
        {
            using (var db = _mdb.GetDataService<dboTypen>())
            {
                var query = $"SELECT sosync_write_date FROM {_mdb.GetStudioModelReadView(studioModelName)} " +
                    $"WHERE {_mdb.GetStudioModelIdentity(studioModelName)} = @studioID;";

                return (await db.ExecuteQueryAsync<DateTime?>(
                    query,
                    new { studioID }))
                    .SingleOrDefault();
            }
        }

        private async Task<DateTime?> GetStudioSosyncWriteDateViaOnlineID(
            string studioModelName,
            int onlineID)
        {
            using (var db = _mdb.GetDataService<dboTypen>())
            {
                var query = $"SELECT sosync_write_date FROM {_mdb.GetStudioModelReadView(studioModelName)} " +
                    $"WHERE sosync_fso_id = @onlineID;";

                return (await db.ExecuteQueryAsync<DateTime?>(
                    query,
                    new { onlineID }))
                    .SingleOrDefault();
            }
        }

        private DateTime? GetOnlineSosyncWriteDateViaOnlineID(
            string onlineModelName,
            int onlineID)
        {
            var onlineData = _odoo.Client.GetDictionary(
                onlineModelName,
                onlineID,
                new[] { "sosync_write_date" });

            return OdooConvert.ToDateTime(
                (string)onlineData["sosync_write_date"],
                true);
        }

        private DateTime? GetOnlineSosyncWriteDateViaStudioID(
            string onlineModelName,
            int studioID)
        {
            var arg = new OdooSearchArgument()
            {
                Field = "sosync_fs_id",
                Operator = "=",
                Value = studioID
            };

            try
            {
                var onlineID = _odoo.Client.SearchBy(onlineModelName, new[] { arg })[0];

                var onlineData = _odoo.Client.GetDictionary(
                    onlineModelName,
                    onlineID,
                    new[] { "sosync_write_date" });

                return OdooConvert.ToDateTime(
                    (string)onlineData["sosync_write_date"],
                    true);
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }
    }
}
