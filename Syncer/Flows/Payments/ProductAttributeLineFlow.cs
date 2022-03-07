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
    [StudioModel(Name = "fson.product_attribute_line")]
    [OnlineModel(Name = "product.attribute.line")]
    public class ProductAttributeLineFlow
        : ReplicateSyncFlow
    {
        public ProductAttributeLineFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonproduct_attribute_line>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var model = Svc.OdooService.Client.GetModel<productAttributeLine>(OnlineModelName, onlineID);

            RequestChildJob(SosyncSystem.FSOnline, "product.attribute", Convert.ToInt32(model.attribute_id[0]), SosyncJobSourceType.Default);
            RequestChildJob(SosyncSystem.FSOnline, "product.template", Convert.ToInt32(model.product_tmpl_id[0]), SosyncJobSourceType.Default);

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<productAttributeLine, fsonproduct_attribute_line>(
                onlineID,
                action,
                studio => studio.product_attribute_lineID,
                (online, studio) =>
                {
                    var attributeID = GetStudioIDFromOnlineReference(
                        "fson.product_attribute",
                        online,
                        x => x.attribute_id,
                        true);

                    var templateID = GetStudioIDFromOnlineReference(
                        "fson.product_template",
                        online,
                        x => x.product_tmpl_id,
                        true);

                    studio.product_attributeID = attributeID;
                    studio.product_templateID = templateID;

                    studio.display_name = online.display_name;
                    studio.fso_write_date = online.write_date;
                    studio.fso_create_date = online.create_date;
                });
        }
    }
}
