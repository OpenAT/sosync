using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Models;
using Syncer.Services;
using WebSosync.Common;
using WebSosync.Data.Models;

namespace Syncer.Flows.zGruppeSystem
{
    [StudioModel(Name = "dbo.zGruppe")]
    [OnlineModel(Name = "frst.zgruppe")]
    public class zGruppeFlow
        : ReplicateSyncFlow
    {
        public zGruppeFlow(SyncServiceCollection svc)
            : base(svc)
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
                    online.Add("ja_gui_anzeige", studio.JaGuianzeige);
                    online.Add("nein_gui_anzeige", studio.NeinGuianzeige);
                    online.Add("gui_gruppen_bearbeiten_moeglich", studio.GUIGruppenBearbeitenMöglich);
                    online.Add("nur_eine_gruppe_anmelden", studio.MehrEinträgeInGruppeMöglich);
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
                    studio.JaGuianzeige = online.ja_gui_anzeige;
                    studio.NeinGuianzeige = online.nein_gui_anzeige;
                    studio.GUIGruppenBearbeitenMöglich = online.gui_gruppen_bearbeiten_moeglich;
                    studio.MehrEinträgeInGruppeMöglich = online.nur_eine_gruppe_anmelden;
                });
        }
    }
}
