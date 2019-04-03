using System;
using System.Collections.Generic;
using System.Text;
using DaDi.Odoo.Models.Payments;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.payment_transaction")]
    [OnlineModel(Name = "payment.transaction")]
    public class PaymentTransactionFlow
        : ReplicateSyncFlow
    {
        public PaymentTransactionFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonpayment_transaction>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<paymentTransaction, fsonpayment_transaction>(
                onlineID,
                action,
                studio => studio.payment_transactionID,
                (online, studio) =>
                {
                    studio.state = online.state;
                    studio.frst_iban = online.frst_iban;
                    studio.frst_bic = online.frst_bic;
                    studio.acquirer_reference = online.acquirer_reference;
                    studio.esr_reference_number = online.esr_reference_number;
                    studio.reference = online.reference;
                    studio.fso_create_date = online.create_date.Value;
                    studio.amount = online.amount;
                });
        }
    }
}
