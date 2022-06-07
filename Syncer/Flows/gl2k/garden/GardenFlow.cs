using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo;
using DaDi.Odoo.Models;
using DaDi.Odoo.Models.GL2K.Garden;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Models;

namespace Syncer.Flows.gl2k.garden
{
    [StudioModel(Name = "fson.garden")]
    [OnlineModel(Name = "gl2k.garden")]
    [SyncTargetStudio, SyncTargetOnline]
    public class GardenFlow
        : ReplicateSyncFlow
    {
        public GardenFlow(SyncServiceCollection svc)
            : base(svc)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsongarden>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = Svc.MdbService.GetDataService<fsongarden>())
            {
                var studioModel = db.Read(new { gardenID = studioID }).SingleOrDefault();
                RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", studioModel.PersonID, SosyncJobSourceType.Default);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = Svc.OdooService.Client.GetDictionary(
                OnlineModelName,
                onlineID,
                new string[] { "partner_id" });

            var odooPartnerID = OdooConvert.ToInt32ForeignKey(odooModel["partner_id"], allowNull: false).Value;

            RequestChildJob(SosyncSystem.FSOnline, "res.partner", odooPartnerID, SosyncJobSourceType.Default);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleTransformToOnline<fsongarden, gl2kGarden>(
                studioID,
                action,
                x => x.gardenID,
                (studio, online) =>
                {
                    // FRST can only alter these fields
                    online.Add("state", studio.state);
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<gl2kGarden, fsongarden>(
                onlineID,
                action,
                x => x.gardenID,
                (online, studio) =>
                {
                    var personID = GetStudioIDFromOnlineReference(
                        "dbo.Person",
                        online,
                        x => x.partner_id,
                        true);

                    var country = Svc.OdooService.Client.GetModel<resCountry>(
                        "res.country",
                        Convert.ToInt32(online.country_id[0]));

                    var landID = Svc.MdbService.GetLandIDFromIsoCode(country.Code).Value;

                    studio.PersonID = personID.Value;
                    studio.state = online.state;
                    studio.TypID = Svc.TypeService.GetTypeID("fsongarden_TypID", online.type);
                    studio.organisation_name = online.organisation_name;
                    studio.email = online.email;
                    studio.newsletter = online.newsletter;
                    studio.salutation = online.salutation;
                    studio.firstname = online.firstname;
                    studio.lastname = online.lastname;
                    studio.zip = online.zip;
                    studio.street = online.street;
                    studio.street_number_web = online.street_number_web;
                    studio.city = online.city;
                    studio.LandID = landID;
                    studio.garden_size = online.garden_size;
                    studio.garden_image_name = online.garden_image_name;
                    studio.garden_image_file = online.garden_image_file;

                    // Computed fields not needed
                    // Login token not needed
                    // Email validation fields not needed

                    studio.fso_create_date = online.create_date;
                });
        }
    }
}
