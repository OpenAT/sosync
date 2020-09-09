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
    [ModelPriority(4000)]
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
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = Svc.OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "cds_id" });
            var cdsId = OdooConvert.ToInt32ForeignKey(odooModel["cds_id"], allowNull: true);
            
            if (cdsId.HasValue)
            {
                RequestChildJob(SosyncSystem.FSOnline, "frst.zverzeichnis", cdsId.Value, SosyncJobSourceType.Default);
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
                        cdsId = GetOnlineID<dbozVerzeichnis>("dbo.zVerzeichnis", "frst.zverzeichnis", studio.zVerzeichnisID.Value);

                    online.Add("cds_id", (object)cdsId ?? false);

                    var personIds = new int[0];
                    using (var dbRel = Svc.MdbService.GetDataService<fsongr_tag_Personen>())
                    {
                        personIds = dbRel.Read(new { gr_tagID = studioID })
                            .Select(rel => rel.PersonID)
                            .ToArray();
                    }

                    // Normal fields
                    online.Add("name", studio.Name);
                    online.Add("type", studio.Typ);
                    online.Add("description", studio.Beschreibung);
                    online.Add("origin", studio.Origin);
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
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
                            "frst.zverzeichnis",
                            "dbo.zVerzeichnis",
                            Convert.ToInt32(online.cds_id[0]))
                            .Value;
                    }
                    studio.zVerzeichnisID = zVerzeichnisID;

                    studio.Name = online.name;
                    studio.Typ = online.type;
                    studio.Beschreibung = online.description;
                    studio.Origin = online.origin;
                });
        }
    }
}
