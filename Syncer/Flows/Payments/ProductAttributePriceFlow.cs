using DaDi.Odoo.Models.Payments;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using WebSosync.Data;
using WebSosync.Data.Constants;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.product_attribute_price")]
    [OnlineModel(Name = "product.attribute.price")]
    [ConcurrencyOnlineWins]
    [SyncTargetStudio]
    public class ProductAttributePriceFlow
        : ReplicateSyncFlow
    {
        public ProductAttributePriceFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonproduct_attribute_price>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var model = Svc.OdooService.Client.GetModel<productAttributePrice>(OnlineModelName, onlineID);

            RequestChildJob(SosyncSystem.FSOnline, "product.attribute.value", Convert.ToInt32(model.value_id[0]), SosyncJobSourceType.Default);
            RequestChildJob(SosyncSystem.FSOnline, "product.template", Convert.ToInt32(model.product_tmpl_id[0]), SosyncJobSourceType.Default);

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<productAttributePrice, fsonproduct_attribute_price>(
                onlineID,
                action,
                studio => studio.product_attribute_priceID,
                (online, studio) =>
                {
                    var valueID = GetStudioIDFromOnlineReference(
                        "fson.product_attribute_value",
                        online,
                        x => x.value_id,
                        true);

                    var templateID = GetStudioIDFromOnlineReference(
                        "fson.product_template",
                        online,
                        x => x.product_tmpl_id,
                        true);

                    studio.product_attribute_valueID = valueID;
                    studio.product_templateID = templateID;

                    studio.display_name = online.display_name;
                    studio.price_extra = online.price_extra;
                    studio.fso_write_date = online.write_date;
                    studio.fso_create_date = online.create_date;
                });
        }
    }
}
