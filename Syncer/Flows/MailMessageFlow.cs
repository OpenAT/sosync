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
            if ((string)mm["model"] == "res.partner")
            {
                var partnerID = OdooConvert.ToInt32((string)mm["res_id"]);
                RequestChildJob(SosyncSystem.FSOnline, "res.partner", partnerID.Value, SosyncJobSourceType.Default);
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
                   int? personID = null;
                   if (online.Model == "res.partner")
                   {
                       personID = GetStudioIDFromOnlineReference(
                            "dbo.Person",
                            online,
                            x => x.ResId,
                            false);
                   }

                   studio.PersonID = personID;
                   studio.subject = online.Subject;
                   studio.body = online.Body;
                   studio.model = online.Model;
                   studio.res_id = online.ResId;
                   studio.record_name = online.RecordName;
                   studio.subtype_xml_id = online.SubtypeXmlId;
                   studio.fso_create_date = online.CreateDate;
               });
        }
    }
}
