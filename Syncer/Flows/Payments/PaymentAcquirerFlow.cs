using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo;
using DaDi.Odoo.Models.Payments;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Models;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.payment_acquirer")]
    [OnlineModel(Name = "payment.acquirer")]
    public class PaymentAcquirerFlow
        : ReplicateSyncFlow
    {
        public PaymentAcquirerFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonpayment_acquirer>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = Svc.OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "frst_xbankverbindung_id" });

            var odooxBankverbindungID = OdooConvert.ToInt32ForeignKey(odooModel["frst_xbankverbindung_id"], allowNull: true);

            if (odooxBankverbindungID.HasValue && odooxBankverbindungID.Value > 0)
                RequestChildJob(SosyncSystem.FSOnline, "frst.xbankverbindung", odooxBankverbindungID.Value, SosyncJobSourceType.Default);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            // Get the referenced Odoo-IDs
            var odooModel = Svc.OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "frst_xbankverbindung_id" });

            var odooxBankverbindungID = OdooConvert.ToInt32ForeignKey(odooModel["frst_xbankverbindung_id"], allowNull: true);

            int? xBankverbindungID = null;
            if (odooxBankverbindungID.HasValue && odooxBankverbindungID.Value > 0)
            {
                xBankverbindungID = GetStudioID<dboxBankverbindung>(
                    "frst.xbankverbindung",
                    "dbo.xBankverbindung",
                    odooxBankverbindungID.Value)
                    .Value;
            }

            SimpleTransformToStudio<paymentAcquirer, fsonpayment_acquirer>(
                onlineID,
                action,
                studio => studio.payment_acquirerID,
                (online, studio) =>
                {
                    studio.name = online.name;
                    studio.do_not_send_status_email = online.do_not_send_status_email;
                    studio.environment = online.environment;
                    studio.globally_hidden = online.globally_hidden;
                    studio.ogonedadi_brand = online.ogonedadi_brand;
                    studio.ogonedadi_pm = online.ogonedadi_pm;
                    studio.ogonedadi_userid = online.ogonedadi_userid;
                    studio.provider = online.provider;
                    studio.recurring_transactions = online.recurring_transactions;
                    studio.redirect_url_after_form_feedback = online.redirect_url_after_form_feedback;
                    studio.validation = online.validation;
                    studio.website_published = online.website_published;
                    studio.xBankverbindungID = xBankverbindungID;
                });
        }
    }
}
