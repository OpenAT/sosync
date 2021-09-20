using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using WebSosync.Common;
using WebSosync.Data.Models;

namespace Syncer.Services
{
    public class SyncServiceCollection
    {
        public ILogger Log { get; set; }
        public OdooService OdooService { get; private set; }
        public MdbService MdbService { get; private set; }
        public TypeService TypeService { get; private set; }
        public OdooFormatService OdooFormat { get; private set; }
        public SerializationService Serializer { get; private set; }
        public SosyncOptions Config { get; private set; }
        public FlowService FlowService { get; private set; }
        public HtmlService HtmlService { get; private set; }

        public SyncServiceCollection(
            OdooService odooSvc,
            SosyncOptions conf,
            FlowService flowService,
            OdooFormatService odooFormatService,
            SerializationService serializationService,
            MdbService mdbService,
            TypeService typeService,
            HtmlService htmlService)
        {
            OdooService = odooSvc;
            Config = conf;
            FlowService = flowService;
            OdooFormat = odooFormatService;
            Serializer = serializationService;
            MdbService = mdbService;
            TypeService = typeService;
            HtmlService = htmlService;
        }
    }
}
