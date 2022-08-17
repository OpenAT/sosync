using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using WebSosync.Data.Models;
using Syncer.Attributes;
using WebSosync.Data.Constants;
using dadi_data.Models;
using System.Linq;
using DaDi.Odoo.Models;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using DaDi.MultiMail.Client;
using Microsoft.Extensions.Logging;
using Syncer.Services;
using WebSosync.Common;

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xTemplate")]
    [OnlineModel(Name = "email.template")]
    [ModelPriority(10000)]
    [SyncTargetStudio]
    public class EmailTemplateFlow
        : ReplicateSyncFlow
    {
        public EmailTemplateFlow(SyncServiceCollection svc)
            : base(svc)
        { }

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var info = GetDefaultOnlineModelInfo(onlineID, OnlineModelName);

            // If there was no foreign ID in fso, try to check the mssql side
            // for the referenced ID too
            if (!info.ForeignID.HasValue)
                info.ForeignID = GetStudioIDFromMssqlViaOnlineID(StudioModelName, Svc.MdbService.GetStudioModelIdentity(StudioModelName), onlineID);

            return info;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            return GetDefaultStudioModelInfo<dboxTemplate>(studioID);
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No child jobs
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // No child jobs
        }

        private const string MssqlOnlyPrefix = "[MSSQL-Only]";

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            // Templates can generally not be synchronized to Odoo.
            //
            // Allow synchronization, but create empty entries in Odoo.
            // This is only required, so template child jobs do not block
            // any parent jobs.
            //
            // The TransformToStudio method must not synchronize such
            // entries, or they would delete template data.

            SimpleTransformToOnline<dboxTemplate, emailTemplate>(
                studioID,
                action,
                s => s.xTemplateID,
                (studio, online) =>
                {
                    online.Add("fso_email_template", true);
                    online.Add("active", false); // Mark inactive, so they don't show up
                    online.Add("name", $"{MssqlOnlyPrefix} {studio.Name}"); // Prefix so sosync2 knows not to sync those back
                });
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var onlineTemplate = Svc.OdooService.Client.GetModel<emailTemplate>(OnlineModelName, onlineID);

            if (!IsValidFsID(onlineTemplate.Sosync_FS_ID))
                onlineTemplate.Sosync_FS_ID = GetStudioIDFromMssqlViaOnlineID(
                    StudioModelName,
                    Svc.MdbService.GetStudioModelIdentity(StudioModelName),
                    onlineID);

            if (onlineTemplate.Name.StartsWith(MssqlOnlyPrefix))
                throw new Exception($"Template is MSSQL-Only, must not synchronize!");

            UpdateSyncSourceData(Svc.OdooService.Client.LastResponseRaw);

            using (var db = Svc.MdbService.GetDataService<dboxTemplate>())
            {
                var orgId = db.ExecuteQuery<int>("select dbo.FS_100_Organisation_01_ID()")
                    .SingleOrDefault();

                if (action == TransformType.CreateNew)
                {
                    var referenceId = PublishToMultiMail(orgId, onlineTemplate);

                    var entry = new dboxTemplate()
                    {
                        Name = onlineTemplate.Name,
                        ReferenzBeschreibung = "message_template_id",
                        ReferenzID = referenceId,

                        EmailBetreff = onlineTemplate.Subject,
                        EmailVon = onlineTemplate.EmailFrom,
                        EmailAntwortAn = onlineTemplate.ReplyTo,
                        EmailHTML = onlineTemplate.FsoEmailHtmlParsed,
                        EmailText = onlineTemplate.FsoEmailText,

                        TemplateFürtypID = 109610, // Für Organisation
                        TemplatetypID = 2005837, // FS-Online Emailtemplate für MultiMail
                        
                        GültigBis = onlineTemplate.Active
                            ? new DateTime(2099, 12, 31)
                            : DateTime.Today.AddDays(-1),
                        
                        xTemplatetypID = 0,

                        Anlagedatum = DateTime.Now,

                        sosync_write_date = (onlineTemplate.Sosync_Write_Date ?? onlineTemplate.Write_Date).Value,
                        sosync_fso_id = onlineID,
                        noSyncJobSwitch = true
                    };

                    UpdateSyncTargetRequest(Svc.Serializer.ToXML(entry));

                    var xTemplateID = 0;
                    try
                    {
                        db.Create(entry);
                        xTemplateID = entry.xTemplateID;
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, xTemplateID);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString(), xTemplateID);
                        throw;
                    }

                    Svc.OdooService.Client.UpdateModel(
                        OnlineModelName,
                        new { sosync_fs_id = entry.xTemplateID },
                        onlineID,
                        false);
                }
                else
                {
                    var sosync_fs_id = onlineTemplate.Sosync_FS_ID;
                    var studioTemplate = db.Read(new { xTemplateID = sosync_fs_id }).SingleOrDefault();

                    UpdateSyncTargetDataBeforeUpdate(Svc.Serializer.ToXML(studioTemplate));

                    var referenceId = studioTemplate.ReferenzID;
                    PublishToMultiMail(orgId, onlineTemplate, referenceId);

                    studioTemplate.Name = onlineTemplate.Name;
                    studioTemplate.EmailBetreff = onlineTemplate.Subject;
                    studioTemplate.EmailVon = onlineTemplate.EmailFrom;
                    studioTemplate.EmailAntwortAn = onlineTemplate.ReplyTo;
                    studioTemplate.EmailHTML = onlineTemplate.FsoEmailHtmlParsed;
                    studioTemplate.EmailText = onlineTemplate.FsoEmailText;

                    if (onlineTemplate.Active == false)
                    {
                        studioTemplate.GültigBis = DateTime.Today.AddDays(-1);
                    }

                    studioTemplate.sosync_write_date = (onlineTemplate.Sosync_Write_Date ?? onlineTemplate.Write_Date).Value;
                    studioTemplate.noSyncJobSwitch = true;

                    UpdateSyncTargetRequest(Svc.Serializer.ToXML(studioTemplate));

                    try
                    {
                        db.Update(studioTemplate);
                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, null);
                    }
                    catch (Exception ex)
                    {
                        UpdateSyncTargetAnswer(ex.ToString(), null);
                        throw;
                    }
                }
            }
        }

        private int PublishToMultiMail(int orgId, emailTemplate onlineTemplate, int referenceId = 0)
        {
            var client = new MultiMailPortTypeClient();
            client.Endpoint.Binding.OpenTimeout = new TimeSpan(0, 0, 10);
            client.Endpoint.Binding.SendTimeout = new TimeSpan(0, 0, 10);

            var task = client.soap_updateMessageTemplateAsync(
                orgId,
                referenceId,
                "mail",
                onlineTemplate.Subject ?? "NO_SUB",
                onlineTemplate.EmailFrom ?? "NO_FROM",
                onlineTemplate.ReplyTo ?? "NO_REPLYTO",
                onlineTemplate.FsoEmailText,
                onlineTemplate.FsoEmailHtmlParsed);

            referenceId = task.GetAwaiter().GetResult();

            return referenceId;
        }
    }
}
