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
using WebSosync.Data.Constants;
using WebSosync.Data.Models;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.payment_transaction")]
    [OnlineModel(Name = "payment.transaction")]
    [ConcurrencyOnlineWins]
    [ModelPriority(4000)]
    [SyncTargetStudio]
    public class PaymentTransactionFlow
        : ReplicateSyncFlow
    {
        public PaymentTransactionFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonpayment_transaction>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
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

                    studio.consale_provider_name = online.consale_provider_name;
                    studio.consale_method = online.consale_method;
                    studio.consale_method_other = online.consale_method_other;
                    studio.consale_method_brand = online.consale_method_brand;
                    studio.consale_method_directdebit_provider = online.consale_method_directdebit_provider;
                    studio.consale_method_account_owner = online.consale_method_account_owner;
                    studio.consale_method_account_iban = online.consale_method_account_iban;
                    studio.consale_method_account_bic = online.consale_method_account_bic;
                    studio.consale_method_account_bank = online.consale_method_account_bank;
                    studio.consale_recurring_payment_provider = online.consale_recurring_payment_provider;
                });
        }
    }
}
