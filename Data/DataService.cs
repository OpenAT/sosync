using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using WebSosync.Data.Models;
using WebSosync.Data.Properties;
using Dapper;
using WebSosync.Data.Helpers;

namespace WebSosync.Data
{
    public class DataService : IDisposable
    {
        #region Constructors
        public DataService(SosyncConfiguration config)
        {
            _con = new NpgsqlConnection(ConnectionHelper.GetPostgresConnectionString(
                config.DB_Host,
                config.DB_Port,
                config.DB_Name,
                config.DB_User,
                config.DB_User_PW));

            _con.Open();
        }
        #endregion

        #region Methods
        public void Setup()
        {
            _con.Execute(Resources.ResourceManager.GetString("SetupDatabase_SCRIPT"));
        }

        public List<SosyncJob> GetSyncJobs()
        {
            var result = _con.Query<SosyncJob>(Resources.ResourceManager.GetString("GetAllOpenSyncJob_SELECT")).AsList();

            return result;
        }
        #endregion

        #region IDisposable implementation
        public void Dispose()
        {
            try
            {
                _con.Close();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _con.Dispose();
            }
        }
        #endregion

        #region Members
        private NpgsqlConnection _con;
        #endregion
    }
}
