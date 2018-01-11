﻿using System;
using System.Collections.Generic;
using System.Text;
using Syncer.Enumerations;
using Syncer.Models;
using Syncer.Attributes;
using System.Reflection;
using dadi_data;
using dadi_data.Models;
using System.Linq;
using Syncer.Exceptions;

namespace Syncer.Flows
{
    /// <summary>
    /// Base for sync flows where source and target models match.
    /// Populate the <see cref="Fields"/> property to specify which
    /// fields to synchronize.
    /// </summary>
    public abstract class UniformSyncFlow : SyncFlow
    {
        #region Constructors
        public UniformSyncFlow(IServiceProvider svc) : base(svc)
        {
            var t = this.GetType();
            var attStudio = t.GetCustomAttribute<StudioModelAttribute>();
            var attOnline = t.GetCustomAttribute<OnlineModelAttribute>();

            StudioModel = attStudio.Name;
            StudioModelID = $"{attStudio.Name.Split('.')[1]}ID";

            OnlineModel = attOnline.Name;
            OnlineModelID = "id";

            Fields = new List<string>();
        }
        #endregion

        #region Methods
        protected override ModelInfo GetOnlineInfo(int onlineID)
        {
            return GetDefaultOnlineModelInfo(onlineID, OnlineModel);
        }

        protected override ModelInfo GetStudioInfo(int studioID)
        {
            using (var db = MdbService.GetDataService<dboTypen>())
            {
                var result = db.ExecuteQuery<ModelInfo>(
                    $"select {StudioModelID} ID, sosync_fso_id ForeignID, write_date WriteDate, sosync_write_date SosyncWriteDate " +
                    $"from {StudioModel} where {StudioModelID} = @studioID",
                    new { studioID = studioID })
                    .SingleOrDefault();

                return result;
            }
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            ThrowIfFieldsMissing();

            // UpdateSyncSourceData(Serializer.ToXML(acc));
            // UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
            // UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, odooCompanyId);
            // UpdateSyncTargetDataBeforeUpdate(OdooService.Client.LastResponseRaw);
            // UpdateSyncTargetRequest(OdooService.Client.LastRequestRaw);
            // UpdateSyncTargetAnswer(OdooService.Client.LastResponseRaw, null);

            throw new NotImplementedException();
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            ThrowIfFieldsMissing();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Throws a <see cref="SyncerException" /> if no <see cref="Fields" /> are specified.
        /// </summary>
        private void ThrowIfFieldsMissing()
        {
            if (Fields.Count <= 0)
                throw new SyncerException($"No fields specified for dynamic flow {this.GetType().Name}.");
        }
        #endregion

        #region Properties
        public List<string> Fields
        {
            get => _fields;
            private set => _fields = value;
        }

        public string StudioModel { get; private set; }
        public string StudioModelID { get; private set; }
        public string OnlineModel { get; private set; }
        public string OnlineModelID { get; private set; }
        #endregion

        #region Members
        private List<string> _fields;
        #endregion
    }
}