using dadi_data.Models;
using Odoo;
using Odoo.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
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
            var partnerID = OdooConvert.ToInt32((string)((List<object>)bpk["BPKRequestPartnerID"])[0]);
            var companyID = OdooConvert.ToInt32((string)((List<object>)bpk["BPKRequestCompanyID"])[0]);

            RequestChildJob(SosyncSystem.FSOnline, "res.company", companyID.Value);
            RequestChildJob(SosyncSystem.FSOnline, "res.partner", partnerID.Value);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            throw new NotSupportedException("bPK entries can only be synced from [fso] to [fs].");
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException("bPK entries can only be synced from [fso] to [fs].");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var bpk = OdooService.Client.GetModel<resPartnerBpk>("res.partner.bpk", onlineID);

            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);

            using (var db = MdbService.GetDataService<dboPersonBPK>())
            {
                if (action == TransformType.CreateNew)
                {
                    var personID = 0;
                    var xBPKAccountID = 0;

                    // Expected to exist due to child jobs
                    using (var dbPers = MdbService.GetDataService<dboPerson>())
                    {
                        var person = dbPers.Read(new { sosync_fso_id = OdooConvert.ToInt32((string)bpk.BPKRequestPartnerID[0]) }).Single();
                        personID = person.PersonID;
                    }

                    // Expected to exist due to child jobs
                    using (var dbAcc = MdbService.GetDataService<dboxBPKAccount>())
                    {
                        var acc = dbAcc.Read(new { sosync_fso_id = OdooConvert.ToInt32((string)bpk.BPKRequestCompanyID[0]) }).Single();
                        xBPKAccountID = acc.xBPKAccountID;
                    }

                    var entry = new dboPersonBPK()
                    {
                        PersonID = personID,
                        xBPKAccountID = xBPKAccountID,
                        Anlagedatum = DateTime.Now,
                        sosync_write_date = (bpk.Sosync_Write_Date ?? bpk.Write_Date).Value,
                        sosync_fso_id = bpk.ID
                    };

                    CopyPartnerBpkToPersonBpk(bpk, entry);
                    entry.noSyncJobSwitch = true;

                    UpdateSyncTargetRequest(Serializer.ToXML(entry));

                    var personBPKID = 0;
                    try
                    {
                        db.Create(entry);
                        personBPKID = entry.PersonBPKID;
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, personBPKID);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString(), personBPKID);
                        throw;
                    }

                    OdooService.Client.UpdateModel(
                        "res.partner.bpk",
                        new { sosync_fs_id = entry.PersonBPKID },
                        onlineID,
                        false);
                }
                else
                {
                    var sosync_fs_id = bpk.Sosync_FS_ID;
                    var entry = db.Read(new { xBPKAccountID = sosync_fs_id }).SingleOrDefault();

                    if (entry == null)
                        throw new ModelNotFoundException(SosyncSystem.FundraisingStudio, "dbo.PersonBPK", sosync_fs_id);

                    UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(entry));

                    CopyPartnerBpkToPersonBpk(bpk, entry);
                    entry.sosync_write_date = bpk.Sosync_Write_Date.Value;
                    entry.noSyncJobSwitch = true;

                    UpdateSyncTargetRequest(Serializer.ToXML(entry));

                    try
                    {
                        db.Update(entry);
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, null);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString(), null);
                        throw;
                    }
                }
            }
        }

        private void CopyPartnerBpkToPersonBpk(resPartnerBpk source, dboPersonBPK dest)
        {
            dest.Anlagedatum = source.Create_Date.ToLocalTime();

            dest.BPKPrivat = source.BPKPrivate;
            dest.BPKOeffentlich = source.BPKPublic;

            dest.Vorname = source.BPKRequestFirstname;
            dest.Nachname = source.BPKRequestLastname;
            dest.Geburtsdatum = source.BPKRequestBirthdate;

            dest.PositivAmUm = source.BPKRequestDate;
            dest.PositivDaten = source.BPKResponseData;

            dest.FehlerAmUm = source.BPKErrorRequestDate;
            dest.FehlerDaten = source.BPKErrorRequestData;
            dest.FehlerAntwortDaten = source.BPKErrorResponseData;
            dest.FehlerNachname = source.BPKErrorRequestLastname;
            dest.FehlerVorname = source.BPKErrorRequestFirstname;
            dest.FehlerGeburtsdatum = source.BPKErrorRequestBirthdate;
            dest.FehlerText = source.BPKErrorText;
            dest.FehlerCode = source.BPKErrorCode;
        }
    }
}
