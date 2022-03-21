using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Data.Models;
using dadi_data.Models;
using DaDi.Odoo.Models.Payments;
using WebSosync.Data;
using WebSosync.Common;

namespace Syncer.Flows.Payments
{
    [StudioModel(Name = "fson.product_payment_interval")]
    [OnlineModel(Name = "product.payment_interval")]
    [ConcurrencyOnlineWins]
    public class ProductPaymentIntervalFlow
        : ReplicateSyncFlow
    {
        public ProductPaymentIntervalFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonproduct_payment_interval>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"{StudioModelName} cannot be transformed to {SosyncSystem.FSOnline.Value}");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<productPaymentInterval, fsonproduct_payment_interval>(
                onlineID,
                action,
                studio => studio.product_payment_intervalID,
                (online, studio) =>
                {
                    studio.name = online.name;
                    studio.xml_id = online.xml_id;
                });
        }
    }
}
