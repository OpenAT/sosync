using dadi_data;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using WebSosync.Data.Models;

namespace Syncer.Services
{
    public class MdbService
    {
        #region Members
        private SosyncOptions _config;
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
        public DataService<TModel> GetDataService<TModel>(SqlConnection con = null, SqlTransaction transaction = null)
        {
            //return new DataService<TModel>($"Data Source=mssql1; Initial Catalog=mdb_careseh_test; Integrated Security=True;");
            if (con == null)
            {
                var conStr = $"Data Source={_config.Studio_MSSQL_Host}; Initial Catalog=mdb_{_config.Instance}; User ID={_config.Studio_Sosync_User}; Password={_config.Studio_Sosync_PW}";
                return new DataService<TModel>(conStr, transaction);
            }
            else
            {
                return new DataService<TModel>(con, transaction);
            }
        }
        #endregion
    }
}
