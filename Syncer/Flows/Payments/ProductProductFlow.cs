using DaDi.Odoo.Models.Payments;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Extensions;
using WebSosync.Data.Models;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.product_product")]
    [OnlineModel(Name = "product.product")]
    [ConcurrencyOnlineWins]
    public class ProductProductFlow
        : ReplicateSyncFlow
    {
        public ProductProductFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonproduct_product>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var model = Svc.OdooService.Client.GetModel<productProduct>(OnlineModelName, onlineID);

            RequestChildJob(SosyncSystem.FSOnline, "product.template", Convert.ToInt32(model.product_tmpl_id[0]), SosyncJobSourceType.Default);

            if (model.attribute_value_ids != null)
            {
                foreach (var detailID in model.attribute_value_ids)
                    RequestChildJob(SosyncSystem.FSOnline, "product.attribute.value", detailID, SosyncJobSourceType.Default);
            }

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<productProduct, fsonproduct_product>(
                onlineID,
                action,
                studio => studio.product_productID,
                (online, studio) =>
                {
                    var product_templateID = GetStudioIDFromOnlineReference(
                        "fson.product_template",
                        online,
                        x => x.product_tmpl_id,
                        true);

                    studio.product_templateID = product_templateID;
                    studio.default_code = online.default_code;
                    studio.fso_create_date = online.create_date.Value;

                    if (studio.product_productID != 0)
                        SaveDetails(studio.product_productID, online.attribute_value_ids);
                },
                null,
                (online, productProductId, studio) => {
                    SaveDetails(studio.product_productID, online.attribute_value_ids);
                });
        }
        
        private void SaveDetails(int studioID, int[] productAttributeValueIDs)
        {
            using (var db = Svc.MdbService.GetDataService<fsonproduct_product>())
            {
                db.MergeProductAttributeValuesProductProductRel(studioID, productAttributeValueIDs);
            }
        }
    }
}
