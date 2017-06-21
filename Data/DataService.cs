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
        private NpgsqlConnection _con;

        public DataService(string conStr)
        {
            _con = new NpgsqlConnection(conStr);
            _con.Open();
        }

        public List<SosyncJob> GetSyncJobs()
        {
            var result = _con.Query<SosyncJob>(Resources.ResourceManager.GetString("OopenSyncJob_SELECT")).AsList();

            return result;
        }

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
    }
}
