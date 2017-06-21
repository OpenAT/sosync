using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using WebSosync.Data.Models;
using WebSosync.Data.Properties;
using Dapper;

namespace WebSosync.Data
{
    public class DataService : IDisposable
    {
        #region Constructors
        public DataService(string conStr)
        {
            _con = new NpgsqlConnection(conStr);
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
