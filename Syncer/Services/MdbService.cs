using dadi_data;
using dadi_data.Models;
using Syncer.Exceptions;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using WebSosync.Data.Models;

namespace Syncer.Services
{
    public class MdbService
    {
        #region Members
        private SosyncOptions _config;
        #endregion

        #region Properties
        public string Instance { get { return _config.Instance; } }
        public List<dboTypen> MdbTypes { get; private set; }
        #endregion

        #region Constructors
        public MdbService(SosyncOptions options)
        {
            _config = options;
            LoadMdbTypes();
        }
        #endregion

        #region Methods
        private void LoadMdbTypes()
        {
            using (var db = GetDataService<dboTypen>())
            {
                MdbTypes = db
                    .Read("SELECT * FROM dbo.Typen", null)
                    .ToList();
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

        public string GetIsoCodeForLandID(int? landID)
        {
            string countryCode = null;

            using (var dbSvc = GetDataService<dboTypen>())
            {
                countryCode = dbSvc.ExecuteQuery<string>(
                    "select sosync.LandID_to_IsoCountryCode2(@LandID)",
                    new { LandID = landID })
                    .FirstOrDefault();
            }

            if (string.IsNullOrEmpty(countryCode))
                return null;

            return countryCode;
        }

        public int? GetLandIDForIsoCode(string isoCode)
        {
            int? landID = null;

            using (var dbSvc = GetDataService<dboTypen>())
            {
                landID = dbSvc.ExecuteQuery<int?>(
                    "select sosync.IsoCountryCode2_to_LandID(@Code)",
                    new { Code = isoCode })
                    .FirstOrDefault();
            }

            if (landID == 0)
                return null;

            return landID;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="DataService{TModel}"/> class, using the
        /// <see cref="SosyncOptions"/> to configure the connection string.
        /// </summary>
        /// <typeparam name="TModel">The MDB model to create the data service for.</typeparam>
        /// <returns></returns>
        public DataService<TModel> GetDataService<TModel>(SqlConnection con = null, SqlTransaction transaction = null) where TModel : MdbModelBase, new()
        {
            //return new DataService<TModel>($"Data Source=mssql1; Initial Catalog=mdb_careseh_test; Integrated Security=True;");
            if (con == null)
            {
                var conStr = string.Join("; ", new[] {
                    $"Data Source={_config.Studio_MSSQL_Host}",
                    $"Initial Catalog=mdb_{_config.Instance}",
                    $"User ID={_config.Studio_Sosync_User}",
                    $"Password ={_config.Studio_Sosync_PW}",
                    $"Connect Timeout=60"
                    });

                return new DataService<TModel>(conStr, 60, true);
            }
            else
            {
                return new DataService<TModel>(con, transaction, 60, true);
            }
        }

        public string GetStudioModelIdentity(string studioModelName)
        {
            if (studioModelName.ToLower().StartsWith("dbo.aktion"))
                return "AktionsID";

            return $"{studioModelName.Split('.')[1]}ID";
        }

        public string GetStudioModelReadView(string studioModelName)
        {
            return $"orm.[{studioModelName.Replace(".", "")}.read.view]";
        }

        public int? GetLandIDFromIsoCode(string isoCode)
        {
            using (var dbSvc = GetDataService<dboTypen>())
            {
                var foundLandID = dbSvc.ExecuteQuery<int?>(
                    "select sosync.IsoCountryCode2_to_LandID(@Code)",
                    new { Code = isoCode })
                    .FirstOrDefault();

                if (foundLandID.HasValue && foundLandID.Value != 0)
                    return foundLandID.Value;
            }

            return null;
        }
        #endregion
    }
}
