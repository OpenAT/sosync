using Dapper;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WebSosync.Data.Models;

namespace Syncer.Services
{
    public class OdooDataService
    {
        private SosyncOptions _config;

        public OdooDataService(SosyncOptions config)
        {
            _config = config;
        }

        private NpgsqlConnection CreateConnection()
        {
            var builder = new NpgsqlConnectionStringBuilder();
            builder.Host = $"pgsql.{_config.Instance}.datadialog.net";
            builder.Port = 5432;
            builder.Database = _config.Instance;
            builder.Username = _config.Instance;
            builder.Password = _config.Online_pgsql_PW;
            builder.Timeout = 15;
            builder.Pooling = true;
            builder.ApplicationName = "sosync2batch";

            return new NpgsqlConnection(builder.ConnectionString);
        }
    }
}
