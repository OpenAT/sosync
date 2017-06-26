using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using WebSosync.Data.Models;
using WebSosync.Data.Properties;
using Dapper;
using WebSosync.Data.Helpers;
using WebSosync.Data.Constants;

namespace WebSosync.Data
{
    public class DataService : IDisposable
    {
        #region Constructors
        public DataService(SosyncOptions config)
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
            _con.Execute(Resources.ResourceManager.GetString(ResourceNames.SetupDatabaseScript));
        }

        public List<SosyncJob> GetSyncJobs()
        {
            var result = _con.Query<SosyncJob>(Resources.ResourceManager.GetString(ResourceNames.GetAllOpenSyncJobsSELECT)).AsList();

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
