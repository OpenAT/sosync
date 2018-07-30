using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.zGruppe")]
    [OnlineModel(Name = "frst.zgruppe")]
    public class zGruppeFlow
        : ReplicateSyncFlow
    {
        public zGruppeFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dbozGruppe>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleTransformToOnline<dbozGruppe, frstzGruppe>(
                studioID,
                action,
                studioModel => studioModel.zGruppeID,
                (studio, online) =>
                    {
                        online.Add("tabellentyp_id", Convert.ToString(studio.TabellentypID));
                        online.Add("gruppe_kurz", studio.GruppeKurz);
                        online.Add("gruppe_lang", studio.GruppeLang);
                        online.Add("gui_anzeigen", studio.GUIAnzeigen);
                    });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<frstzGruppe, dbozGruppe>(
                onlineID,
                action,
                studioModel => studioModel.zGruppeID,
                (online, studio) =>
                    {
                        studio.TabellentypID = online.tabellentyp_id;
                        studio.GruppeKurz = online.gruppe_kurz;
                        studio.GruppeLang = online.gruppe_lang;
                        studio.GUIAnzeigen = online.gui_anzeigen;
                    });
        }
    }
}
