using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Data.Helpers
{
    public static class ConnectionHelper
    {
        public static string GetPostgresConnectionString(string instance, string user, string pass)
        {
#warning TODO: Replace localhost with instance-DNS URL
            return $"Host=localhost;Username={user};Password={pass};Database={instance}";
        }
    }
}
