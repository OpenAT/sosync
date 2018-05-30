//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using DaDi.Odoo.Models;
//using dadi_data.Models;
//using Syncer.Attributes;
//using Syncer.Enumerations;
//using Syncer.Models;
//using WebSosync.Data.Models;

//namespace Syncer.Flows
//{
//    [StudioModel(Name = "fson.res_groups")]
//    [OnlineModel(Name = "res.groups")]
//    public class GroupsFlow
//        : ReplicateSyncFlow
//    {
//        public GroupsFlow(IServiceProvider svc, SosyncOptions conf) : base(svc, conf)
//        { }

//        protected override ModelInfo GetOnlineInfo(int onlineID)
//        {
//            var info = GetDefaultOnlineModelInfo(onlineID, OnlineModelName);

//            // If there was no foreign ID in fso, try to check the mssql side
//            // for the referenced ID too
//            if (!info.ForeignID.HasValue)
//                info.ForeignID = GetFsIdByFsoId(StudioModelName, MdbService.GetStudioModelIdentity(StudioModelName), onlineID);

//            return info;
//        }

//        protected override ModelInfo GetStudioInfo(int studioID)
//        {
//            using (var db = MdbService.GetDataService<fsonres_groups>())
//            {
//                var studioGroup = db.Read(new { res_groupsID = studioID }).SingleOrDefault();
//                if (studioGroup != null)
//                {
//                    if (!studioGroup.sosync_fso_id.HasValue)
//                        studioGroup.sosync_fso_id = GetFsoIdByFsId(OnlineModelName, studioGroup.res_groupsID);

//                    return new ModelInfo(studioID, studioGroup.sosync_fso_id, studioGroup.sosync_write_date, studioGroup.write_date);
//                }
//            }

//            return null;
//        }

//        protected override void SetupOnlineToStudioChildJobs(int onlineID)
//        {
//            // No child jobs for groups
//        }

//        protected override void SetupStudioToOnlineChildJobs(int studioID)
//        {
//            // No child jobs for groups
//        }

//        protected override void TransformToOnline(int studioID, TransformType action)
//        {
//            fsonres_groups studioGroup = null;
//            using (var db = MdbService.GetDataService<fsonres_groups>())
//            {
//                studioGroup = db.Read(new { res_groupsID = studioID }).SingleOrDefault();

//                if (!studioGroup.sosync_fso_id.HasValue)
//                    studioGroup.sosync_fso_id = GetFsoIdByFsId(OnlineModelName, studioGroup.res_groupsID);

//                UpdateSyncSourceData(Serializer.ToXML(studioGroup));

//                // Perpare data that is the same for create or update
//                var data = new Dictionary<string, object>()
//                {
//                    { "name", studioGroup.name },
//                    { "full_name", studioGroup.full_name },
//                    { "sosync_write_date", (studioGroup.sosync_write_date ?? studioGroup.write_date.ToUniversalTime()) }
//                };

//                if (action == TransformType.CreateNew)
//                {
//                    data.Add("sosync_fs_id", studioGroup.res_groupsID);
//                    int odooGroupsId = 0;
//                    try
//                    {
//                        odooGroupsId = OdooService.Client.CreateModel(OnlineModelName, data, false);
//                        studioGroup.sosync_fso_id = odooGroupsId;
//                        db.Update(studioGroup);
//                    }
//                    finally
//                    {
//                        UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
//                        UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, odooGroupsId);
//                    }
//                }
//                else
//                {
//                    var onlineGroup = OdooService.Client.GetModel<resGroups>(OnlineModelName, studioGroup.sosync_fso_id.Value);

//                    UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);
//                    try
//                    {
//                        OdooService.Client.UpdateModel(OnlineModelName, data, studioGroup.sosync_fso_id.Value, false);
//                    }
//                    finally
//                    {
//                        UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
//                        UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, null);
//                    }
//                }
//            }
//        }

//        protected override void TransformToStudio(int onlineID, TransformType action)
//        {
//            resGroups onlineGroup = OdooService.Client.GetModel<resGroups>(OnlineModelName, onlineID);

//            if (!IsValidFsID(onlineGroup.Sosync_FS_ID))
//                onlineGroup.Sosync_FS_ID = GetFsIdByFsoId(StudioModelName, MdbService.GetStudioModelIdentity(StudioModelName), onlineID);

//            UpdateSyncSourceData(OdooService.Client.LastResponseRaw);

//            using (var db = MdbService.GetDataService<fsonres_groups>())
//            {
//                if (action == TransformType.CreateNew)
//                {
//                    var entry = new fsonres_groups()
//                    {
//                        name = onlineGroup.Name,
//                        full_name = onlineGroup.Full_Name,
//                        sosync_write_date = (onlineGroup.Sosync_Write_Date ?? onlineGroup.Write_Date).Value,
//                        sosync_fso_id = onlineID,
//                        noSyncJobSwitch = true
//                    };

//                    UpdateSyncTargetRequest(Serializer.ToXML(entry));

//                    var groupsID = 0;
//                    try
//                    {
//                        db.Create(entry);
//                        groupsID = entry.res_groupsID;
//                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, groupsID);
//                    }
//                    catch (Exception ex)
//                    {
//                        UpdateSyncTargetAnswer(ex.ToString(), groupsID);
//                        throw;
//                    }

//                    OdooService.Client.UpdateModel(
//                        OnlineModelName,
//                        new { sosync_fs_id = entry.res_groupsID },
//                        onlineID,
//                        false);
//                }
//                else
//                {
//                    var sosync_fs_id = onlineGroup.Sosync_FS_ID;
//                    var studioGroup = db.Read(new { res_groupsID = sosync_fs_id }).SingleOrDefault();

//                    UpdateSyncTargetDataBeforeUpdate(Serializer.ToXML(studioGroup));

//                    studioGroup.name = onlineGroup.Name;
//                    studioGroup.full_name = onlineGroup.Full_Name;
//                    studioGroup.sosync_write_date = onlineGroup.Sosync_Write_Date ?? onlineGroup.Write_Date;
//                    studioGroup.noSyncJobSwitch = true;

//                    UpdateSyncTargetRequest(Serializer.ToXML(studioGroup));

//                    try
//                    {
//                        db.Update(studioGroup);
//                        UpdateSyncTargetAnswer(MssqlTargetSuccessMessage, null);
//                    }
//                    catch (Exception ex)
//                    {
//                        UpdateSyncTargetAnswer(ex.ToString(), null);
//                        throw;
//                    }
//                }
//            }
//        }
//    }
//}
