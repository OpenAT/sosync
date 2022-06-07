using DaDi.Odoo;
using DaDi.Odoo.Models;
using dadi_data.Models;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using WebSosync.Data;
using WebSosync.Data.Constants;

namespace Syncer.Flows
{
    [StudioModel(Name = "fson.mail_message")]
    [OnlineModel(Name = "mail.message")]
    [SyncTargetStudio]
    public class MailMessageFlow
        : ReplicateSyncFlow
    {
        public MailMessageFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<fsonmail_message>(studioID);
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            // Kein sync nach FSO
            throw new NotSupportedException();
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            var mm = Svc.OdooService.Client.GetDictionary("mail.message", onlineID, new string[] { "model", "res_id" });
            var odooModel = (string)mm["model"];
            var resId = OdooConvert.ToInt32((string)mm["res_id"]);

            var modelIsInSync = Svc.FlowService.FsoModelMap.ContainsKey(odooModel);
            var resIdPresent = resId != null && resId.Value > 0;

            if (modelIsInSync && resIdPresent)
            {
                RequestChildJob(SosyncSystem.FSOnline, odooModel, resId.Value, SosyncJobSourceType.Default);
            }
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleTransformToStudio<MailMessage, fsonmail_message>(
               onlineID,
               action,
               studioModel => studioModel.mail_messageID,
               (online, studio) =>
               {
                   int? resFsID = null;

                   if (Svc.FlowService.FsoModelMap.ContainsKey(online.Model))
                   {
                       var studioModelName = Svc.FlowService.FsoModelMap[online.Model];
                       resFsID = GetStudioIDFromOnlineReference(
                            studioModelName,
                            online,
                            x => x.ResId,
                            false);
                   }

                   studio.ResFsID = resFsID;
                   studio.subject = online.Subject;

                   studio.body = online.Body;
                   studio.bodyText = Svc.HtmlService.GetPlainTextFromPartialHtml(online.Body);

                   studio.model = online.Model;
                   studio.res_id = online.ResId;
                   studio.record_name = online.RecordName;
                   studio.subtype_xml_id = online.SubtypeXmlId;
                   studio.fso_create_date = online.CreateDate;
                   studio.AnlageAmUm = studio.AnlageAmUm ?? DateTime.Now;
               });
        }
    }
}
