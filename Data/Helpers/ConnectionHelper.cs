using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Data.Helpers
{
    public static class ConnectionHelper
    {
        public static string GetPostgresConnectionString(string host, int port, string database, string user, string pass)
        {
            return $"Host={host};Port={port};Database={database};Username={user};Password={pass};Timeout=60;SSL=True";
        }
    }
}
