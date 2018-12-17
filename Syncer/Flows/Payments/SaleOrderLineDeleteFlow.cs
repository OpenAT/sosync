﻿using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.sale_order_line")]
    [OnlineModel(Name = "sale.order_line")]
    public class SaleOrderLineDeleteFlow
        : DeleteSyncFlow
    {
        public SaleOrderLineDeleteFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService) : base(logger, odooService, conf, flowService)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<fsonsale_order_line>(onlineID);
        }
    }
}
