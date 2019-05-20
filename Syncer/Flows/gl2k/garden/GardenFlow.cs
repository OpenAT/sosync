using System;
using System.Collections.Generic;
using System.Text;
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
using WebSosync.Data.Models;

namespace Syncer.Flows.gl2k.garden
{
    [StudioModel(Name = "fson.garden")]
    [OnlineModel(Name = "gl2k.garden")]
    public class GardenFlow
        : ReplicateSyncFlow
    {
        public GardenFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService) : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsongarden>(studioID);
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

                    var country = OdooService.Client.GetModel<resCountry>(
                        "res.country",
                        Convert.ToInt32(online.country_id[0]));

                    var landID = MdbService.GetLandIDFromIsoCode(country.Code).Value;

                    studio.PersonID = personID.Value;
                    studio.state = online.state;
                    studio.email = online.email;
                    studio.newsletter = online.newsletter;
                    studio.salutation = online.salutation;
                    studio.firstname = online.firstname;
                    studio.lastname = online.lastname;
                    studio.zip = online.zip;
                    studio.street = online.street;
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
