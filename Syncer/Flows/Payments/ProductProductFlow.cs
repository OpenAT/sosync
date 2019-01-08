﻿using DaDi.Odoo.Models.Payments;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.product_product")]
    [OnlineModel(Name = "product.product")]
    public class ProductProductFlow
        : ReplicateSyncFlow
    {
        public ProductProductFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService) : base(logger, odooService, conf, flowService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonproduct_product>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline}");
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var templateId = GetOnlineReferenceID(
                OnlineModelName,
                onlineID,
                nameof(productProduct.product_tmpl_id));

            RequestChildJob(SosyncSystem.FSOnline, "product.template", templateId.Value);

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<productProduct, fsonproduct_product>(
                onlineID,
                action,
                studio => studio.product_productID,
                (online, studio) =>
                {
                    int? product_templateID;

                    var studioModel = "fson.product_template";
                    product_templateID = GetStudioIDFromMssqlViaOnlineID(
                        studioModel,
                        MdbService.GetStudioModelIdentity(studioModel),
                        Convert.ToInt32(online.product_tmpl_id[0]));

                    studio.product_templateID = product_templateID;
                    studio.default_code = online.default_code;
                    studio.fso_create_date = online.create_date.Value;
                });
        }
    }
}
