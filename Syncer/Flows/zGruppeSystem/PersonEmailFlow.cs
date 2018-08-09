//using System;
//using System.Collections.Generic;
//using System.Text;
//using DaDi.Odoo.Models;
//using dadi_data.Models;
//using Syncer.Attributes;
//using Syncer.Enumerations;
//using Syncer.Models;
//using WebSosync.Common;
//using WebSosync.Data.Models;

//namespace Syncer.Flows.zGruppeSystem
//{
//    [StudioModel(Name = "dbo.PersonEmail")]
//    [OnlineModel(Name = "frst.personemail")]
//    public class PersonEmailFlow
//        : ReplicateSyncFlow
//    {
//        public PersonEmailFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
//        {
//        }

//        protected override ModelInfo GetStudioInfo(int studioID)
//        {
//            return GetDefaultStudioModelInfo<dboPersonEmail>(studioID);
//        }

//        protected override void TransformToOnline(int studioID, TransformType action)
//        {
//            var partnerID = 0;

//            SimpleTransformToOnline<dboPersonEmail, frstPersonemail>(
//                studioID,
//                action,
//                studioModel => studioModel.PersonEmailID,
//                (studio, online) =>
//                    {
//                        online.Add("email", EmailHelper.MergeEmail(studio.EmailVor, studio.EmailNach));
//                        // last_email_update
//                        online.Add("partner_id", partnerID);
//                        online.Add("gueltig_von", null);
//                        online.Add("gueltig_bis", null);
//                        // state
//                        // main_address
//                    });
//        }

//        protected override void TransformToStudio(int onlineID, TransformType action)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
