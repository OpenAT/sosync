namespace WebSosync.Data.Models
{
    /// <summary>
    /// Represents the possible configuration options for sosync.
    /// </summary>
    public class SosyncOptions
    {
        public bool? Active { get; set; }
        public int Port { get; set; }

        public string Instance { get; set; }

        public string DB_Host { get; set; }
        public int DB_Port { get; set; }
        public string DB_Name { get; set; }
        public string DB_User { get; set; }
        public string DB_User_PW { get; set; }

        public string Online_DB_Host { get; set; }
        public int Online_DB_Port { get; set; }
        public string Online_DB_Name { get; set; }
        public string Online_DB_User { get; set; }
        public string Online_DB_User_PW { get; set; }
        
        public int Token_Batch_Size{ get; set; }

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
        public int? Max_Threads { get; set; }
        public int? Job_Package_Size { get; set; }
        public int? Model_Lock_Timeout { get; set; }
    }
}