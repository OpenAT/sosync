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
        /// <summary>
        /// Creates a new instance of the <see cref="DataService"/> class. Takes <see cref="SosyncOptions"/>
        /// to initialize the database connection.
        /// </summary>
        /// <param name="config">The settings for the database connection.</param>
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
        /// <summary>
        /// Runs the database creation script on the database.
        /// </summary>
        public void Setup()
        {
            _con.Execute(Resources.ResourceManager.GetString(ResourceNames.SetupDatabaseScript));
        }

        /// <summary>
        /// Reads all open SyncJobs from the database.
        /// </summary>
        /// <returns></returns>
        public List<SosyncJob> GetSyncJobs()
        {
            var result = _con.Query<SosyncJob>(Resources.ResourceManager.GetString(ResourceNames.GetAllOpenSyncJobsSELECT)).AsList();
            return result;
        }
        #endregion

        #region IDisposable implementation
        /// <summary>
        /// Closes the database connection and disposes the connection.
        /// </summary>
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
