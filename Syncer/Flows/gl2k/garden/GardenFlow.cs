using System;
using System.Collections.Generic;
using System.Text;
using DaDi.Odoo.Models;
using DaDi.Odoo.Models.GL2K.Garden;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.gl2k.garden
{
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
            throw new NotSupportedException($"Model {StudioModelName} can only be synchronized from {SosyncSystem.FSOnline} to {SosyncSystem.FundraisingStudio}.");
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

                    studio.cmp_image_file = online.cmp_image_file;
                    studio.cmp_thumbnail_file = online.cmp_thumbnail_file;
                    studio.cmp_better_zip_id = Convert.ToInt32(online.cmp_better_zip_id[0]);
                    studio.cmp_state_id = Convert.ToInt32(online.cmp_state_id[0]);
                    studio.cmp_county_province = online.cmp_county_province;
                    studio.cmp_county_province_code = online.cmp_county_province_code;
                    studio.cmp_community = online.cmp_community;
                    studio.cmp_community_code = online.cmp_community_code;
                    studio.cmp_city = online.cmp_city;
                    studio.cmp_latitude = online.cmp_latitude;
                    studio.cmp_longitude = online.cmp_longitude;

                    studio.login_token_used = online.login_token_used;

                    studio.email_validate = online.email_validate;
                    studio.email_validate_token = online.email_validate_token;
                    studio.email_validated_at = online.email_validated_at;

                    studio.fso_create_date = online.create_date;
                });
        }
    }
}
