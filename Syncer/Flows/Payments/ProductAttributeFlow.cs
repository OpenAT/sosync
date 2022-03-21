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
    [StudioModel(Name = "fson.product_attribute")]
    [OnlineModel(Name = "product.attribute")]
    [ConcurrencyOnlineWins]
    public class ProductAttributeFlow
        : ReplicateSyncFlow
    {
        public ProductAttributeFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonproduct_attribute>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<productAttribute, fsonproduct_attribute>(
                onlineID,
                action,
                studio => studio.product_attributeID,
                (online, studio) =>
                {
                    studio.name = online.name;
                    studio.display_name = online.display_name;
                    studio.fso_write_date = online.write_date;
                    studio.fso_create_date = online.create_date;
                });
        }
    }
}
