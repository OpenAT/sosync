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
    [StudioModel(Name = "fson.sale_order_line")]
    [OnlineModel(Name = "sale.order.line")]
    public class SaleOrderLineFlow
        : ReplicateSyncFlow
    {
        public SaleOrderLineFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService) : base(logger, odooService, conf, flowService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonsale_order_line>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline}");
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var model = OdooService.Client.GetModel<saleOrderLine>(OnlineModelName, onlineID);

            RequestChildJob(SosyncSystem.FSOnline, "sale.order", Convert.ToInt32(model.order_id[0]));
            RequestChildJob(SosyncSystem.FSOnline, "product.product", Convert.ToInt32(model.product_id[0]));

            if (model.payment_interval_id != null && model.payment_interval_id.Length > 1)
                RequestChildJob(SosyncSystem.FSOnline, "product.payment_interval", Convert.ToInt32(model.payment_interval_id[0]));

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<saleOrderLine, fsonsale_order_line>(
                onlineID,
                action,
                studio => studio.sale_order_lineID,
                (online, studio) =>
                {
                    var orderID = GetStudioIDFromOnlineReference(
                        "fson.sale_order",
                        online,
                        x => x.order_id,
                        true);

                    var productID = GetStudioIDFromOnlineReference(
                        "fson.product_product",
                        online,
                        x => x.product_id,
                        false);

                    var intervalID = GetStudioIDFromOnlineReference(
                        "fson.product_payment_interval",
                        online,
                        x => x.payment_interval_id,
                        false);

                    studio.sale_orderID = orderID;
                    studio.product_productID = productID;
                    studio.product_payment_intervalID = intervalID;

                    studio.price_donate = online.price_donate;
                    studio.price_unit = online.price_unit;
                    studio.product_uos_qty = online.product_uos_qty;
                    studio.state = online.state;
                    studio.fs_ptoken = online.fs_ptoken;
                    studio.fs_origin = online.fs_origin;
                });
        }
    }
}