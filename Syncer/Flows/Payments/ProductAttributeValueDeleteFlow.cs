﻿using DaDi.Odoo.Models.Payments;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using System;
using WebSosync.Data;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.product_attribute_value")]
    [OnlineModel(Name = "product.attribute.value")]
    public class ProductAttributeValueDeleteFlow
        : DeleteSyncFlow
    {
        public ProductAttributeValueDeleteFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<fsonproduct_attribute_value>(onlineID);
        }
    }
}
