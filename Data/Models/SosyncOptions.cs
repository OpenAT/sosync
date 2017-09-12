namespace WebSosync.Data.Models
{
    /// <summary>
    /// Represents the possible configuration options for sosync.
    /// </summary>
    public class SosyncOptions
    {
        /// <summary>
        /// Port for Kestrel webserver.
        /// </summary>
        public int Port { get; set; }

        public string Instance { get; set; }

        public string DB_Host { get; set; }
        public int DB_Port { get; set; }
        public string DB_Name { get; set; }
        public string DB_User { get; set; }
        public string DB_User_PW { get; set; }

        public string Log_File { get; set; }
        public string Log_Level { get; set; }

        public string Studio_MSSQL_Host { get; set; }
        public string Studio_Sosync_User { get; set; }
        public string Studio_Sosync_PW { get; set; }

        public string Online_Host { get; set; }
        public string Online_Sosync_User { get; set; }
        public string Online_Sosync_PW { get; set; }

        public int Throttle_ms { get; set; }
        public int Protocol_Throttle_ms { get; set; }

        public int Max_Time_Drift_ms { get; set; }
    }
}