using dadi_data.Models;
using Syncer.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSosync.Data.Models;

namespace Syncer.Services
{
    public class TypeService
        : MssqlServiceBase
    {
        private object _lock = new object();

        public List<dboTypen> MdbTypes { get; private set; }

        public TypeService(SosyncOptions config)
            : base(config)
        {
            LoadMdbTypes();
        }

        private void LoadMdbTypes()
        {
            using (var db = GetDataService<dboTypen>())
            {
                lock (_lock)
                {
                    MdbTypes = db
                        .Read("SELECT * FROM dbo.Typen WITH (NOLOCK)", null)
                        .ToList();
                }
            }
        }

        public string GetTypeValue(int? typeID)
        {
            if (typeID.HasValue)
            {
                return MdbTypes
                        .Where(x => x.TypenID == typeID.Value)
                        .SingleOrDefault()
                        .Wert;
            }

            return null;
        }

        public int? GetTypeID(string typeDescription, string value)
        {
            if (value == null)
                return null;

            var mdbType = MdbTypes
                .Where(x => x.TypenBezeichnung == typeDescription && x.Wert == value)
                .SingleOrDefault();

            if (mdbType == null)
                throw new SyncerException($"Type not found for TypenBezeichnung = \"{typeDescription ?? "<NULL>"}\", Wert = \"{value ?? "<NULL>"}\"");

            return mdbType.TypenID;
        }
    }
}
