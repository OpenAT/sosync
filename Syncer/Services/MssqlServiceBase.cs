using dadi_data;
using dadi_data.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using WebSosync.Data.Models;

namespace Syncer.Services
{
    public class MssqlServiceBase
    {
        protected SosyncOptions Config;

        public MssqlServiceBase(SosyncOptions config)
        {
            Config = config;
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
                    $"Data Source={Config.Studio_MSSQL_Host}",
                    $"Initial Catalog=mdb_{Config.Instance}",
                    $"User ID={Config.Studio_Sosync_User}",
                    $"Password ={Config.Studio_Sosync_PW}",
                    $"Connect Timeout=60"
                    });

                return new DataService<TModel>(conStr, 60, true);
            }
            else
            {
                return new DataService<TModel>(con, transaction, 60, true);
            }
        }
    }
}
