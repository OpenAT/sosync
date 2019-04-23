using DaDi.Odoo.Models.Payments;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Models;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.sale_order")]
    [OnlineModel(Name = "sale.order")]
    [ModelPriority(ModelPriority.P4)]
    public class SaleOrderFlow
        : ReplicateSyncFlow
    {
        public SaleOrderFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonsale_order>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var online = OdooService.Client.GetModel<saleOrder>(OnlineModelName, onlineID);

            RequestChildJob(SosyncSystem.FSOnline, "res.partner", Convert.ToInt32(online.partner_id[0]));

            if (online.payment_tx_id != null && online.payment_tx_id.Length > 1)
                RequestChildJob(SosyncSystem.FSOnline, "payment.transaction", Convert.ToInt32(online.payment_tx_id[0]));

            if (online.payment_acquirer_id != null && online.payment_acquirer_id.Length > 1)
                RequestChildJob(SosyncSystem.FSOnline, "payment.acquirer", Convert.ToInt32(online.payment_acquirer_id[0]));

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<saleOrder, fsonsale_order>(
                onlineID,
                action,
                studio => studio.sale_orderID,
                (online, studio) =>
                {
                    var personID = GetStudioIDFromOnlineReference(
                        "dbo.Person",
                        online,
                        x => x.partner_id,
                        true);

                    var paymentTransactionID = GetStudioIDFromOnlineReference(
                        "fson.payment_transaction",
                        online,
                        x => x.payment_tx_id,
                        false);

                    var acquirerID = GetStudioIDFromOnlineReference(
                        "fson.payment_acquirer",
                        online,
                        x => x.payment_acquirer_id,
                        false);

                    studio.PersonID = personID;
                    studio.payment_transactionID = paymentTransactionID;
                    studio.payment_acquirerID = acquirerID;

                    studio.fso_create_date = online.create_date.Value;
                    studio.date_order = online.date_order;
                    studio.amount_total = online.amount_total;
                    studio.state = online.state;
                });
        }
    }
}
