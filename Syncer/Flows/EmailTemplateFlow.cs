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

namespace Syncer.Flows
{
    [StudioModel(Name = "dbo.xTemplate")]
    [OnlineModel(Name = "email.template")]
    [ModelPriority(ModelPriority.High)]
    public class EmailTemplateFlow
        : ReplicateSyncFlow
    {
        public EmailTemplateFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
        { }

        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            var info = GetDefaultOnlineModelInfo(onlineID, OnlineModelName);

            // If there was no foreign ID in fso, try to check the mssql side
            // for the referenced ID too
            if (!info.ForeignID.HasValue)
                info.ForeignID = GetFsIdByFsoId(StudioModelName, MdbService.GetStudioModelIdentity(StudioModelName), onlineID);

            return info;
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            using (var db = MdbService.GetDataService<dboxTemplate>())
            {
                var emailTemplate = db.Read(new { xTemplateID = studioID }).SingleOrDefault();
                if (emailTemplate != null)
                {
                    if (!emailTemplate.sosync_fso_id.HasValue)
                        emailTemplate.sosync_fso_id = GetFsoIdByFsId(OnlineModelName, emailTemplate.xTemplateID);

                    return new ModelInfo(studioID, emailTemplate.sosync_fso_id, emailTemplate.sosync_write_date, emailTemplate.write_date);
                }
            }

            return null;
        }

        protected override void SetupOnlineToStudioChildJobs(int onlineID)
        {
            // No child jobs
        }

        protected override void SetupStudioToOnlineChildJobs(int studioID)
        {
            // No child jobs
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException(
                $"{StudioModelName} can only be synchronized from [FSO] -> [FS].");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            var onlineTemplate = OdooService.Client.GetModel<emailTemplate>(OnlineModelName, onlineID);

            if (!IsValidFsID(onlineTemplate.Sosync_FS_ID))
                onlineTemplate.Sosync_FS_ID = GetFsIdByFsoId(
                    StudioModelName,
                    MdbService.GetStudioModelIdentity(StudioModelName),
                    onlineID);

            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);

            using (var db = MdbService.GetDataService<dboxTemplate>())
            {
                if (action == TransformType.CreateNew)
                {
                    var entry = new dboxTemplate()
                    {
                        Name = onlineTemplate.Name,

                        EmailBetreff = onlineTemplate.Subject,
                        EmailVon = onlineTemplate.EmailFrom,
                        EmailAntwortAn = onlineTemplate.ReplyTo,
                        EmailHTML = InsertLinkTrackers(onlineTemplate.FsoEmailHtmlParsed),

                        TemplateFürtypID = 109610, // Für Organisation
                        TemplatetypID = 2005837, // FS-Online Emailtemplate für MultiMail
                        GültigBis = new DateTime(2099, 12, 31),
                        xTemplatetypID = 0,

                        Anlagedatum = DateTime.Now,

                        sosync_write_date = (onlineTemplate.Sosync_Write_Date ?? onlineTemplate.Write_Date).Value,
                        sosync_fso_id = onlineID,
                        noSyncJobSwitch = true
                    };

                    UpdateSyncTargetRequest(Serializer.ToXML(entry));

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

                    OdooService.Client.UpdateModel(
                        OnlineModelName,
                        new { sosync_fs_id = entry.xTemplateID },
                        onlineID,
                        false);
                }
                else
                {
                    var sosync_fs_id = onlineTemplate.Sosync_FS_ID;
                    var studioTemplate = db.Read(new { xTemplateID = sosync_fs_id }).SingleOrDefault();

                    UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(studioTemplate));

                    studioTemplate.Name = onlineTemplate.Name;
                    studioTemplate.EmailBetreff = onlineTemplate.Subject;
                    studioTemplate.EmailVon = onlineTemplate.EmailFrom;
                    studioTemplate.EmailAntwortAn = onlineTemplate.ReplyTo;
                    studioTemplate.EmailHTML = InsertLinkTrackers(onlineTemplate.FsoEmailHtmlParsed);

                    studioTemplate.sosync_write_date = onlineTemplate.Sosync_Write_Date ?? onlineTemplate.Write_Date;
                    studioTemplate.noSyncJobSwitch = true;

                    UpdateSyncTargetRequest(Serializer.ToXML(studioTemplate));

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

        private string InsertLinkTrackers(string html)
        {
            // do not track links with class "dadi_notrack"

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            foreach (var linkNode in doc.DocumentNode.SelectNodes("//a[@href]"))
                if (IsTrackableLink(linkNode))
                    linkNode.Attributes["href"].Value = CreateTrackingLink(linkNode.Attributes["href"].Value);

            return doc.DocumentNode.OuterHtml;
        }

        private bool IsTrackableLink(HtmlNode node)
        {
            var isAnchor = node.Name.ToLower() == "a";
            var hasHref = node.Attributes.Contains("href");
            var isNoTrack = node.Attributes.Contains("class") && node.Attributes["class"].Value.ToLower().Contains("dadi_notrack");

            return isAnchor & hasHref & !isNoTrack;
        }

        private string CreateTrackingLink(string link)
        {
            // Do not touch Hash-References
            if (link.StartsWith("#"))
                return link;

            // Links without http or https default to https
            if (!link.ToLower().StartsWith("http"))
                return $"%redirector%/https//{link}";

            var exp = new Regex("https?://", RegexOptions.IgnoreCase);
            var match = exp.Match(link);

            if (match.Value.ToLower().Contains("s"))
                link = link.Replace(match.Value, "%redirector%/https//");
            else
                link = link.Replace(match.Value, "%redirector%/http//");

            return link;
        }
    }
}
