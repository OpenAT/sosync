using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo.Models.Payments;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.Payments
{
    public class PaymentAcquirerFlow
        : ReplicateSyncFlow
    {
        public PaymentAcquirerFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService) : base(logger, odooService, conf, flowService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonpayment_acquirer>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<paymentAcquirer, fsonpayment_acquirer>(
                onlineID,
                action,
                studio => studio.payment_acquirerID,
                (online, studio) =>
                {
                    studio.name = online.name;
                    studio.do_not_send_status_email = online.do_not_send_status_email;

                    studio.sel_key__environment = online.environment.Keys.First();
                    studio.sel_val__environment = online.environment[online.environment.Keys.First()];

                    studio.globally_hidden = online.globally_hidden;
                    studio.ogonedadi_brand = online.ogonedadi_brand;
                    studio.ogonedadi_pm = online.ogonedadi_pm;
                    studio.ogonedadi_userid = online.ogonedadi_userid;

                    studio.sel_key__provider = online.provider.Keys.First();
                    studio.sel_val__provider = online.provider[online.provider.Keys.First()];

                    studio.recurring_transactions = online.recurring_transactions;
                    studio.redirect_url_after_form_feedback = online.redirect_url_after_form_feedback;

                    studio.sel_key__validation = online.validation.Keys.First();
                    studio.sel_val__validation = online.validation[online.validation.Keys.First()];

                    studio.website_published = online.website_published;
                });
        }
    }
}
