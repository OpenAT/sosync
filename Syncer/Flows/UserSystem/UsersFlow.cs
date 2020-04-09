//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using DaDi.Odoo;
//using dadi_data.Models;
//using Syncer.Attributes;
//using Syncer.Enumerations;
//using Syncer.Models;
//using WebSosync.Data;
//using WebSosync.Data.Models;

//namespace Syncer.Flows.UserSystem
//{
//    [StudioModel(Name = "fson.res_users")]
//    [OnlineModel(Name = "res.users")]
//    public class UsersFlow
//        : ReplicateSyncFlow
//    {
//        public UsersFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
//        { }

//        protected override ModelInfo GetOnlineInfo(int onlineID)
//        {
//            var info = GetDefaultOnlineModelInfo(onlineID, OnlineModelName);

//            // If there was no foreign ID in fso, try to check the mssql side
//            // for the referenced ID too
//            if (!info.ForeignID.HasValue)
//                info.ForeignID = GetFsIdByFsoId(StudioModelName, Svc.MdbService.GetStudioModelIdentity(StudioModelName), onlineID);

//            return info;
//        }

//        protected override ModelInfo GetStudioInfo(int studioID)
//        {
//            using (var db = Svc.MdbService.GetDataService<fsonres_users>())
//            using (var db2 = Svc.MdbService.GetDataService<fsonres_groups_users_rel>())
//            {
//                var studioUser = db.Read(new { res_usersID = studioID }).SingleOrDefault();
//                var studioRel = db2.Read(new { res_usersID = studioID }).SingleOrDefault();

//                if (studioUser != null)
//                {
//                    if (!studioUser.sosync_fso_id.HasValue)
//                        studioUser.sosync_fso_id = GetFsoIdByFsId(OnlineModelName, studioUser.res_usersID);

//                    var sosync_write_date = new[] { studioUser.sosync_write_date, studioRel.sosync_write_date }.Max();

//                    return new ModelInfo(studioID, studioUser.sosync_fso_id, sosync_write_date, studioUser.write_date);
//                }
//            }

//            return null;
//        }

//        protected override void SetupOnlineToStudioChildJobs(int onlineID)
//        {
//            var user = Svc.OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "partner_id" });
//            var partnerID = OdooConvert.ToInt32((string)((List<object>)user["partner_id"])[0]);

//            RequestChildJob(SosyncSystem.FSOnline, "res.partner", partnerID.Value);
//        }

//        protected override void SetupStudioToOnlineChildJobs(int studioID)
//        {
//            using (var db = Svc.MdbService.GetDataService<fsonres_users>())
//            {
//                var studioUser = db.Read(new { res_usersID = studioID })
//                    .SingleOrDefault();

//                if (studioUser.PersonID.HasValue)
//                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", studioUser.PersonID.Value);

//#warning TODO: Setup child jobs for each referenced group
//            }
//        }

//        protected override void TransformToOnline(int studioID, TransformType action)
//        {
//            throw new NotImplementedException();
//        }

//        protected override void TransformToStudio(int onlineID, TransformType action)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
