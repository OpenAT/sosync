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
using System.Reflection;
using System.Linq;

namespace WebSosync.Data
{
    public class DataService : IDisposable
    {
        #region Members
        private static string SQL_CreatJob;
        private static string SQL_UpdateJob;

        private NpgsqlConnection _con;
        #endregion

        #region Class initializers
        /// <summary>
        /// Class initializer. Prepares the sync job insert statement.
        /// </summary>
        static DataService()
        {
            // List of properties to be excluded from the statement generation (primary key, relational properties, etc.)
            var excludedColumns = new string[] { "job_id", "children" };

            var properties = typeof(SyncJob).GetProperties()
                .Where(x => !excludedColumns.Contains(x.Name.ToLower()));

            // Generate the insert statement
            //   insert into sync_table (prop1, prop2, ...) values (@prop1, @prop2, ...)
            SQL_CreatJob = $"insert into sync_table (\n\t{string.Join(",\n\t", properties.Select(x => x.Name.ToLower() == "end" ? "\"end\"" : x.Name.ToLower()))}\n) values (\n\t{string.Join(",\n\t", properties.Select(x => $"@{x.Name.ToLower()}"))}\n);\nSELECT currval(pg_get_serial_sequence('sync_table','job_id'));";

            // Update statement
            //   update sync_table set prop1 = @prop1, prop2 = @prop2, ... where job_id = @job_id
            SQL_UpdateJob = $"update sync_table set\n\t{string.Join(",\n\t", properties.Select(x => x.Name.ToLower() == "end" ? "\"end\" = @end" : $"{x.Name.ToLower()} = @{x.Name.ToLower()}"))}\nwhere job_id = @job_id)";
        }
        #endregion

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
        public List<SyncJob> GetJobs()
        {
            var result = _con.Query<SyncJob>(Resources.ResourceManager.GetString(ResourceNames.GetAllOpenSyncJobsSelect)).AsList();
            return result;
        }

        /// <summary>
        /// Creates a new sync job in the database.
        /// </summary>
        /// <param name="job">The sync job to be created.</param>
        public void CreateJob(SyncJob job)
        {
            // The insert statement is dynamically created as a static value in the class initializer,
            // hence it is not read from resources
            job.Job_ID = _con.Query<int>(DataService.SQL_CreatJob, job).Single();
        }

        /// <summary>
        /// Updates the sync in the database.
        /// </summary>
        /// <param name="job">The sync job to be updated.</param>
        public void UpdateJob(SyncJob job)
        {
            _con.Execute(DataService.SQL_UpdateJob, job);
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
    }
}
