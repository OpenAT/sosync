using dadi_data.Models;
using Microsoft.Extensions.Logging;
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
    public class PartnerBpkFlow : ReplicateSyncFlow
    {
        #region Members
        private ILogger<PartnerBpkFlow> _log;
        #endregion

        #region Constructors
        public PartnerBpkFlow(IServiceProvider svc)
            : base(svc)
        {
            _log = (ILogger<PartnerBpkFlow>)svc.GetService(typeof(ILogger<PartnerBpkFlow>));
        }
        #endregion

        #region Methods
        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var info = GetDefaultOnlineModelInfo(onlineID, "res.partner.bpk");

            // If there was no foreign ID in fso, try to check the mssql side
            // for the referenced ID too
            if (!info.ForeignID.HasValue)
                info.ForeignID = GetFsIdByFsoId("dbo.PersonBPK", "PersonBPKID", onlineID);

            return info;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            using (var db = MdbService.GetDataService<dboPersonBPK>())
            {
                var bpk = db.Read(new { PersonBPKID = studioID }).SingleOrDefault();
                if (bpk != null)
                {
                    if (!bpk.sosync_fso_id.HasValue)
                        bpk.sosync_fso_id = GetFsoIdByFsId("res.partner.bpk", bpk.PersonBPKID);

                    return new ModelInfo(studioID, bpk.sosync_fso_id, bpk.sosync_write_date, bpk.write_date);
                }
            }

            return null;
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var bpk = OdooService.Client.GetDictionary("res.partner.bpk", onlineID, new string[] { "bpk_request_partner_id", "bpk_request_company_id" });
            var partnerID = OdooConvert.ToInt32((string)((List<object>)bpk["bpk_request_partner_id"])[0]);
            var companyID = OdooConvert.ToInt32((string)((List<object>)bpk["bpk_request_company_id"])[0]);

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

            if (!IsValidFsID(bpk.Sosync_FS_ID))
                bpk.Sosync_FS_ID = GetFsIdByFsoId("dbo.PersonBPK", "PersonBPKID", onlineID);

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
                        var person = dbPers.Read(new { sosync_fso_id = OdooConvert.ToInt32((string)bpk.bpk_request_partner_id[0]) }).Single();
                        personID = person.PersonID;
                    }

                    // Expected to exist due to child jobs
                    using (var dbAcc = MdbService.GetDataService<dboxBPKAccount>())
                    {
                        var acc = dbAcc.Read(new { sosync_fso_id = OdooConvert.ToInt32((string)bpk.bpk_request_company_id[0]) }).Single();
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
                    var sosync_fs_id = bpk.Sosync_FS_ID.Value;
                    var entry = db.Read(new { PersonBPKID = sosync_fs_id }).SingleOrDefault();

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

            dest.BPKPrivat = source.bpk_private;
            dest.BPKOeffentlich = source.bpk_public;

            dest.Vorname = source.bpk_request_firstname;
            dest.Nachname = source.bpk_request_lastname;
            dest.Geburtsdatum = source.bpk_request_birthdate;

            dest.PositivAmUm = source.bpk_request_date;
            dest.PositivDaten = source.bpk_response_data;

            dest.FehlerAmUm = source.bpk_error_request_date;
            dest.FehlerDaten = source.bpk_error_request_data;
            dest.FehlerAntwortDaten = source.bpk_error_response_data;
            dest.FehlerNachname = source.bpk_error_request_lastname;
            dest.FehlerVorname = source.bpk_eror_request_firstname;
            dest.FehlerGeburtsdatum = source.bpk_error_request_birthdate;
            dest.FehlerText = source.bpk_error_text;
            dest.FehlerCode = source.bpk_error_code;
        }
        #endregion
    }
}
