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
    [StudioModel(Name = "fson.product_category")]
    [OnlineModel(Name = "product.category")]
    public class ProductCategoryFlow
        : ReplicateSyncFlow
    {
        public ProductCategoryFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonproduct_category>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<productCategory, fsonproduct_category>(
                onlineID,
                action,
                studio => studio.product_categoryID,
                (online, studio) =>
                {
                    studio.name = online.name;
                    studio.display_name = online.display_name;
                    studio.fso_create_date = online.create_date;
                    studio.fso_write_date = online.write_date;
                    studio.AnlageAmUm = studio.AnlageAmUm == DateTime.MinValue
                        ? DateTime.Now
                        : studio.AnlageAmUm;
                });
        }
    }
}
