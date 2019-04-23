using System;
using System.Collections.Generic;
using System.Text;
using dadi_data.Models;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Services;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Models;

namespace Syncer.Flows.gl2k.garden
{
    [StudioModel(Name = "fson.garden")]
    [OnlineModel(Name = "gl2k.garden")]
    public class GardenDeleteFlow
        : DeleteSyncFlow
    {
        public GardenDeleteFlow(ILogger logger, OdooService odooService, SosyncOptions conf, FlowService flowService, OdooFormatService odooFormatService, SerializationService serializationService) : base(logger, odooService, conf, flowService, odooFormatService, serializationService)
        {
        }

        protected override void TransformToOnline(int studioID, TransformType action)
        {
            throw new NotSupportedException($"Deletion is only supported from {SosyncSystem.FSOnline.Value} to {SosyncSystem.FundraisingStudio.Value}.");
        }

        protected override void TransformToStudio(int onlineID, TransformType action)
        {
            SimpleDeleteInStudio<fsongarden>(onlineID);
        }
    }
}
