using DaDi.Odoo;
using DaDi.Odoo.Models.CDS;
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
using WebSosync.Data.Models;

namespace Syncer.Flows.CDS
{
    [StudioModel(Name = "dbo.zVerzeichnis")]
    [OnlineModel(Name = "frst.zverzeichnis")]
    public class zVerzeichnisFlow
        : ReplicateSyncFlow
    {
        public zVerzeichnisFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService)
            : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dbozVerzeichnis>(studioID);
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            using (var db = MdbService.GetDataService<dbozVerzeichnis>())
            {
                var studioModel = db.Read(new { zVerzeichnisID = studioID }).SingleOrDefault();

                if (studioModel.zVerzeichnisIDParent.HasValue)
                    RequestChildJob(SosyncSystem.FundraisingStudio, StudioModelName, studioModel.zVerzeichnisIDParent.Value);
            }
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var odooModel = OdooService.Client.GetDictionary(OnlineModelName, onlineID, new string[] { "parent_id" });
            var parentID = OdooConvert.ToInt32ForeignKey(odooModel["parent_id"], allowNull: true);

            if (parentID.HasValue)
                RequestChildJob(SosyncSystem.FSOnline, OnlineModelName, parentID.Value);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            SimpleTransformToOnline<dbozVerzeichnis, frstzVerzeichnis>(
                studioID,
                action,
                studio => studio.zVerzeichnisID,
                (studio, online) =>
                {
                    online.Add("verzeichnisname", studio.VerzeichnisName);
                    online.Add("verzeichnislang", studio.VerzeichnisLang);
                    online.Add("verzeichniskuerzel", studio.Verzeichniskürzel);
                    online.Add("bemerkung", studio.Bemerkung);

                    int? parentVerzeichnisID = null;

                    if (studio.zVerzeichnisIDParent.HasValue)
                        parentVerzeichnisID = GetOnlineID<dbozVerzeichnis>(StudioModelName, OnlineModelName, studio.zVerzeichnisIDParent.Value);

                    online.Add("parent_id", (object)parentVerzeichnisID ?? false);

                    var isFolder = studio.VerzeichnistypID == 110200;
                    online.Add("verzeichnistyp_id", isFolder);
                    online.Add("bezeichnungstyp_id", MdbService.GetTypeValue(studio.BezeichnungstypID));

                    online.Add("anlagedatum", studio.Anlagedatum);
                    online.Add("startdatum", studio.Startdatum);
                    online.Add("endedatum", studio.Endedatum);
                    online.Add("verantwortlicher_benutzer", studio.VerantwortlichBenutzer);
                    online.Add("fibukontonummer", studio.FibuKontonummer);

                    online.Add("cdsdokument", studio.CDSDokument);

                    // xBankverbindung is not synchronized, thus simply map foreign key as value
                    online.Add("xbankverbindungidfuereinzugsvertraege", studio.xBankverbindungIDFürEinzugsverträge);

                    // Obsolete
                    
                    // This foreign key is simply mapped 1:1 as value. Odoo defines this property read-only
                    // due to it beeing deprecated. So we do not bother with child jobs etc.
                    online.Add("uebersteigendebeitraegeprojahraufspendenzverzeichnisid", studio.ÜbersteigendeBeiträgeproJahraufSpendenzVerzeichnisID);
                    
                    online.Add("verwendungalszmarketingid", studio.VerwendungAlszMarketingID);
                    online.Add("sorterinhierarchie", studio.SorterinHierarchie);
                    online.Add("organisationseinheit", studio.Organisationseinheit);
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<frstzVerzeichnis, dbozVerzeichnis>(
                onlineID,
                action,
                studio => studio.zVerzeichnisID,
                (online, studio) =>
                {
                    studio.VerzeichnisName = online.verzeichnisname;
                    studio.VerzeichnisLang = online.verzeichnislang ?? "";
                    studio.Verzeichniskürzel = online.verzeichniskuerzel ?? "";
                    studio.Bemerkung = online.bemerkung;

                    int? parentVerzeichnisID = null;
                    if (online.parent_id != null)
                    {
                        parentVerzeichnisID = GetStudioID<dbozVerzeichnis>(
                            OnlineModelName,
                            StudioModelName,
                            Convert.ToInt32(online.parent_id[0]))
                            .Value;
                    }
                    studio.zVerzeichnisIDParent = parentVerzeichnisID;

                    var isFolder = online.verzeichnistyp_id;
                    studio.VerzeichnistypID = isFolder ? 110200 : 110202; // 110200=Folder, 110202=List

                    studio.BezeichnungstypID = MdbService.GetTypeID(
                        "zVerzeichnis_BezeichnungstypID",
                        online.bezeichnungstyp_id)
                        .Value;

                    studio.Anlagedatum = online.anlagedatum;
                    studio.Startdatum = online.startdatum;
                    studio.Endedatum = online.endedatum;
                    studio.VerantwortlichBenutzer = online.verantwortlicher_benutzer;
                    studio.FibuKontonummer = online.fibukontonummer;
                    studio.CDSDokument = online.cdsdokument;

                    studio.xBankverbindungIDFürEinzugsverträge = online.xbankverbindungidfuereinzugsvertraege;

                    // Obsolete

                    studio.ÜbersteigendeBeiträgeproJahraufSpendenzVerzeichnisID = online.uebersteigendebeitraegeprojahraufspendenzverzeichnisid;
                    studio.VerwendungAlszMarketingID = online.verwendungalszmarketingid;
                    studio.SorterinHierarchie = online.sorterinhierarchie;
                    studio.Organisationseinheit = online.organisationseinheit;
                });
        }
    }
}
