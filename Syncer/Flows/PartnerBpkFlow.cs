using DaDi.Odoo;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.PersonBPK")]
    [OnlineModel(Name = "res.partner.bpk")]
    [ModelPriority(2000)]
    public class PartnerBpkFlow : ReplicateSyncFlow
    {
        #region Constructors
        public PartnerBpkFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }
        #endregion

        #region Methods
        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboPersonBPK>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var bpk = Svc.OdooService.Client.GetDictionary("res.partner.bpk", onlineID, new string[] { "bpk_request_partner_id", "bpk_request_company_id" });
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
            SimpleTransformToStudio<resPartnerBpk, dboPersonBPK>(
                onlineID,
                action,
                x => x.PersonBPKID,
                (online, studio) =>
                {
                    var personID = GetStudioIDFromOnlineReference(
                        "dbo.Person",
                        online,
                        x => x.bpk_request_partner_id,
                        true);

                    var xBPKAccountID = GetStudioIDFromOnlineReference(
                        "dbo.xBPKAccount",
                        online,
                        x => x.bpk_request_company_id,
                        true);

                    studio.PersonID = personID.Value;
                    studio.xBPKAccountID = xBPKAccountID.Value;

                    studio.Anlagedatum = online.Create_Date.ToLocalTime();

                    studio.BPKPrivat = online.bpk_private;
                    studio.BPKOeffentlich = online.bpk_public;

                    studio.Vorname = online.bpk_request_firstname;
                    studio.Nachname = online.bpk_request_lastname;
                    studio.Geburtsdatum = online.bpk_request_birthdate;
                    studio.PLZ = online.bpk_request_zip;
                    studio.Strasse = online.bpk_request_street;
                    studio.FehlerStrasse = online.bpk_error_request_street;

                    studio.PositivAmUm = online.bpk_request_date;
                    studio.PositivDaten = online.bpk_response_data;

                    studio.FehlerAmUm = online.bpk_error_request_date;
                    studio.FehlerDaten = online.bpk_error_request_data;
                    studio.FehlerAntwortDaten = online.bpk_error_response_data;
                    studio.FehlerNachname = online.bpk_error_request_lastname;
                    studio.FehlerVorname = online.bpk_error_request_firstname;
                    studio.FehlerGeburtsdatum = online.bpk_error_request_birthdate;
                    studio.FehlerPLZ = online.bpk_error_request_zip;
                    studio.FehlerText = online.bpk_error_text;
                    studio.FehlerCode = online.bpk_error_code;
                    studio.RequestLog = online.bpk_request_log;
                    studio.LastRequest = online.last_bpk_request;
                    studio.RequestURL = online.bpk_request_url;
                    studio.ErrorRequestURL = online.bpk_error_request_url;
                    studio.fso_state = online.state;
                });
        }
        #endregion
    }
}
