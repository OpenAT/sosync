﻿using dadi_data;
using dadi_data.Models;
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
        #endregion

        #region Constructors
        public MdbService(SosyncOptions options)
        {
            _config = options;
        }
        #endregion

        #region Methods
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
