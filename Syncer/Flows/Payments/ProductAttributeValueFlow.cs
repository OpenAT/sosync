using DaDi.Odoo.Models.Payments;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using WebSosync.Data;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.product_attribute_value")]
    [OnlineModel(Name = "product.attribute.value")]
    public class ProductAttributeValueFlow
        : ReplicateSyncFlow
    {
        public ProductAttributeValueFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonproduct_attribute_value>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<productAttributeValue, fsonproduct_attribute_value>(
                onlineID,
                action,
                studio => studio.product_attribute_valueID,
                (online, studio) =>
                {
                    studio.name = online.name;
                    studio.display_name = online.display_name;
                    studio.color = online.color;
                    studio.price_extra = online.price_extra;
                    studio.sequence = online.sequence;
                    studio.fso_write_date = online.write_date;
                    studio.fso_create_date = online.create_date;
                });
        }
    }
}
