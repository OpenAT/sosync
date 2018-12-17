using DaDi.Odoo;
using DaDi.Odoo.Models.Payments;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.product_template")]
    [OnlineModel(Name = "product.template")]
    public class ProductTemplateFlow
        : ReplicateSyncFlow
    {
        public ProductTemplateFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService) : base(logger, odooService, conf, flowService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonproduct_template>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline}");
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooPaymentIntervalID = GetOnlineReferenceID(
                OnlineModelName, 
                onlineID,
                nameof(productTemplate.payment_interval_default));

            if (odooPaymentIntervalID.HasValue)
                RequestChildJob(SosyncSystem.FSOnline, "product.payment_interval", odooPaymentIntervalID.Value);

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<productTemplate, fsonproduct_template>(
                onlineID,
                action,
                studio => studio.product_templateID,
                (online, studio) =>
                {
                    int? product_payment_intervalID;

                    var studioModel = "fson.product_payment_interval";
                    product_payment_intervalID = GetStudioIDFromMssqlViaOnlineID(
                        studioModel, 
                        MdbService.GetStudioModelIdentity(studioModel),
                        (int)online.payment_interval_default[0]);
                   
                    studio.name = online.name;
                    studio.product_payment_intervalID__payment_interval_default = product_payment_intervalID;
                    studio.fs_product_type = online.fs_product_type;
                    studio.product_page_template = online.product_page_template;
                    studio.type = online.type;
                    studio.active = online.active;
                    studio.description_sale = online.description_sale;
                    studio.website_url = online.website_url;
                    studio.list_price = online.list_price;
                    studio.price_donate = online.price_donate;
                    studio.price_donate_min = online.price_donate_min;
                    studio.website_published = online.website_published;
                    studio.website_published_start = online.website_published_start;
                    studio.website_published_end = online.website_published_end;
                    studio.website_visible = online.website_visible;
                });
        }
    }
}
