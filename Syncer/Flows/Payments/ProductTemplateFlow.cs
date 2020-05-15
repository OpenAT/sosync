using DaDi.Odoo;
using DaDi.Odoo.Models.Payments;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Extensions;
using WebSosync.Data.Models;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.product_template")]
    [OnlineModel(Name = "product.template")]
    public class ProductTemplateFlow
        : ReplicateSyncFlow
    {
        public ProductTemplateFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonproduct_template>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = Svc.OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "payment_interval_default", "zgruppedetail_ids" });
            var paymentIntervalDefaultID = OdooConvert.ToInt32ForeignKey(odooModel["payment_interval_default"], allowNull: true);

            if (paymentIntervalDefaultID.HasValue)
                RequestChildJob(SosyncSystem.FSOnline, "product.payment_interval", paymentIntervalDefaultID.Value, SosyncJobSourceType.Default);

            if (odooModel["zgruppedetail_ids"] != null)
            {
                foreach (var zgdIdObj in (List<object>)odooModel["zgruppedetail_ids"])
                {
                    var zgdId = Convert.ToInt32(zgdIdObj);

                    if (zgdId > 0)
                        RequestChildJob(SosyncSystem.FSOnline, "frst.zgruppedetail", zgdId, SosyncJobSourceType.Default);
                }
            }

            base.SetupOnlineToStudioChildJobs(onlineID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be synced to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<productTemplate, fsonproduct_template>(
                onlineID,
                action,
                studio => studio.product_templateID,
                (online, studio) =>
                {
                    int? product_payment_intervalID = null;

                    var studioModel = "fson.product_payment_interval";
                    if (online.payment_interval_default != null)
                        product_payment_intervalID = GetStudioIDFromMssqlViaOnlineID(
                            studioModel, 
                            Svc.MdbService.GetStudioModelIdentity(studioModel),
                            Convert.ToInt32(online.payment_interval_default[0]));
                   
                    studio.name = online.name;
                    studio.product_payment_intervalID__payment_interval_default = product_payment_intervalID;
                    studio.fs_product_type = online.fs_product_type;
                    studio.product_page_template = online.product_page_template;
                    studio.type = online.type;
                    studio.active = online.active;
                    studio.description_sale = online.description_sale;
                    studio.website_url = online.website_url;
                    studio.list_price = online.list_price;
                    studio.price_donate = online.price_donate;
                    studio.price_donate_min = online.price_donate_min;
                    studio.website_published = online.website_published;
                    studio.website_published_start = online.website_published_start;
                    studio.website_published_end = online.website_published_end;
                    studio.website_visible = online.website_visible;
                    studio.default_code = online.default_code;

                    SaveDetails(studio.product_templateID, online.zgruppedetail_ids);
                });
        }

        private void SaveDetails(int studioID, int[] onlineDetailIDs)
        {
            using (var db = Svc.MdbService.GetDataService<dbozGruppeDetail>())
            {
                db.MergeProductTemplateGroups(studioID, onlineDetailIDs);
            }
        }
    }
}
