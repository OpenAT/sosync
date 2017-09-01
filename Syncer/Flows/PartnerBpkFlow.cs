﻿using dadi_data.Models;
using Odoo;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using WebSosync.Data;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.PersonBPK")]
    [OnlineModel(Name = "res.partner.bpk")]
    public class PartnerBpkFlow : SyncFlow
    {
        public PartnerBpkFlow(IServiceProvider svc)
            : base(svc)
        { }

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            return GetDefaultOnlineModelInfo(onlineID, "res.partner.bpk");
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            using (var db = MdbService.GetDataService<dboPersonBPK>())
            {
                var bpk = db.Read(new { PersonBPKID = studioID }).SingleOrDefault();
                if (bpk != null)
                    return new ModelInfo(studioID, bpk.sosync_fso_id, bpk.sosync_write_date, bpk.write_date);
            }

            return null;
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var bpk = OdooService.Client.GetDictionary("res.partner.bpk", onlineID, new string[] { "BPKRequestPartnerID", "BPKRequestCompanyID" });
            var partnerID = OdooConvert.ToInt32((string)bpk["BPKRequestPartnerID"]);
            var companyID = OdooConvert.ToInt32((string)bpk["BPKRequestCompanyID"]);

            RequestChildJob(SosyncSystem.FSOnline, "res.company", companyID.Value);
            RequestChildJob(SosyncSystem.FSOnline, "res.partner", partnerID.Value);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = MdbService.GetDataService<dboPersonBPK>())
            {
                var bpk = db.Read(new { PersonBPKID = studioID }).SingleOrDefault();

                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.xBPKAccount", bpk.xBPKAccountID);
                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", bpk.PersonID);
            }
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotImplementedException();

            // Just realized... do this later, if at all, because for BPK the
            // direction should in theory always be FSO -> FS


            //using (var db = MdbService.GetDataService<dboPersonBPK>())
            //{
            //    var bpk = db.Read(new { PersonBPKID = studioID }).SingleOrDefault();

            //    var data = new Dictionary<string, object>()
            //    {
            //        { "BPKPrivate", bpk.BPKPrivat },
            //        { "BPKPublic", bpk.BPKOeffentlich },
            //        { "BPKRequestFirstname", bpk.Vorname },
            //        { "BPKRequestLastname", bpk.Nachname },
            //        { "", bpk.Geburtsdatum },

            //        { "", bpk.PositivAmUm },
            //        { "", bpk.PositivDaten },
            //        { "", bpk.PositivAntwortAmUm },
            //        { "", bpk.PositivAntwortDaten },

            //        { "", bpk.Geburtsdatum },
            //        { "", bpk.Geburtsdatum },
            //        { "", bpk.Geburtsdatum },
            //        { "", bpk.Geburtsdatum },
            //        { "", bpk.Geburtsdatum },
            //        { "", bpk.Geburtsdatum },
            //        { "", bpk.Geburtsdatum },
            //        { "", bpk.Geburtsdatum },
            //        { "", bpk.Geburtsdatum }

            //    };
            //}
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {



            throw new NotImplementedException();
        }
    }
}