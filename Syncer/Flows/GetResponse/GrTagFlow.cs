using DaDi.Odoo;
using DaDi.Odoo.Models.GetResponse;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Extensions;

namespace Syncer.Flows.GetResponse
{
    [StudioModel(Name = "fson.gr_tag")]
    [OnlineModel(Name = "gr.tag")]
    public class GrTagFlow
        : ReplicateSyncFlow
    {
        public GrTagFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsongr_tag>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = Svc.MdbService.GetDataService<fsongr_tag>())
            using (var dbRel = Svc.MdbService.GetDataService<fsongr_tag_Personen>())
            {
                var studioModel = db.Read(new { gr_tagID = studioID }).SingleOrDefault();

                if (studioModel.zVerzeichnisID.HasValue)
                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.zVerzeichnis", studioModel.zVerzeichnisID.Value, SosyncJobSourceType.Default);

                var relModels = dbRel.Read(new { gr_tagID = studioID })
                    .ToList();

                foreach (var relation in relModels)
                {
                    RequestChildJob(SosyncSystem.FundraisingStudio, "dbo.Person", relation.PersonID, SosyncJobSourceType.Default);
                }
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = Svc.OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "cds_id", "partner_ids" });
            var cdsId = OdooConvert.ToInt32ForeignKey(odooModel["cds_id"], allowNull: true);
            var partnerIds = new int[0];
            
            if (odooModel["zgruppedetail_ids"] != null)
            {
                partnerIds = ((List<object>)odooModel["zgruppedetail_ids"])
                    .Select(x => Convert.ToInt32(x))
                    .ToArray();
            }

            if (cdsId.HasValue)
            {
                RequestChildJob(SosyncSystem.FSOnline, "frst.zverzeichnis", cdsId.Value, SosyncJobSourceType.Default);
            }

            foreach (var partnerId in partnerIds)
            {
                RequestChildJob(SosyncSystem.FSOnline, "res.partner", partnerId, SosyncJobSourceType.Default);
            }
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleTransformToOnline<fsongr_tag, grTag>(
                studioID,
                action,
                studio => studio.gr_tagID,
                (studio, online) =>
                {
                    // CDS FK
                    int? cdsId = null;

                    if (studio.zVerzeichnisID.HasValue)
                        cdsId = GetOnlineID<dbozVerzeichnis>(StudioModelName, OnlineModelName, studio.zVerzeichnisID.Value);

                    online.Add("cds_id", (object)cdsId ?? false);

                    var personIds = new int[0];
                    using (var dbRel = Svc.MdbService.GetDataService<fsongr_tag_Personen>())
                    {
                        personIds = dbRel.Read(new { gr_tagID = studioID })
                            .Select(rel => rel.PersonID)
                            .ToArray();
                    }

                    // Partner relations
                    var partnerIds = personIds
                        .Select(personId => GetOnlineID<dboPerson>("dbo.Person", "res.partner", personId))
                        .ToArray();

                    if (partnerIds.Length > 0)
                    {
                        online.Add("partner_ids", partnerIds);
                    }
                    else
                    {
                        online.Add("partner_ids", false);
                    }

                    // Normal fields
                    online.Add("name", studio.Name);
                    online.Add("type", "system"); // Tags from FS are always "system"
                    // FS has no description
                    // FS has no origin
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            int[] online_partner_ids = null;

            SimpleTransformToStudio<grTag, fsongr_tag>(
                onlineID,
                action,
                studio => studio.gr_tagID,
                (online, studio) =>
                {
                    int? zVerzeichnisID = null;
                    if (online.cds_id != null)
                    {
                        zVerzeichnisID = GetStudioID<dbozVerzeichnis>(
                            OnlineModelName,
                            StudioModelName,
                            Convert.ToInt32(online.cds_id[0]))
                            .Value;
                    }
                    studio.zVerzeichnisID = zVerzeichnisID;

                    studio.Name = online.name;

                    online_partner_ids = online.partner_ids;
                });

            var grTagId = GetStudioIDFromMssqlViaOnlineID(
                StudioModelName,
                Svc.MdbService.GetStudioModelIdentity(StudioModelName),
                onlineID);

            SaveDetails(grTagId.Value, online_partner_ids);
        }

        private void SaveDetails(int studioID, int[] onlineDetailIDs)
        {
            using (var db = Svc.MdbService.GetDataService<fsongr_tag_Personen>())
            {
                db.MergeGrTagPersons(studioID, onlineDetailIDs);
            }
        }
    }
}
