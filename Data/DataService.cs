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
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Data;

namespace WebSosync.Data
{
    public class DataService : IDisposable
    {
        #region Constants
        private const string DuplicateTableError = "42P07";
        private const string UndefinedObjectError = "42704";
        #endregion

        #region Properties
        public NpgsqlConnection Connection { get { return _con; } }
        #endregion

        #region Members
        private static string SQL_CreateJob;
        private static string SQL_UpdateJob;

        private NpgsqlConnection _con;
        private int _cmdTimeoutSec = 120;
        private SosyncOptions _config;
        #endregion

        #region Class initializers
        /// <summary>
        /// Class initializer. Prepares the sync job insert statement.
        /// </summary>
        static DataService()
        {
            // List of properties to be excluded from the statement generation (primary key, relational properties, etc.)
            var excludedColumns = new string[] { "id", "children" };

            var properties = typeof(SyncJob).GetProperties()
                .Where(x => !excludedColumns.Contains(x.Name.ToLower()));

            // Generate the insert statement
            //   insert into sosync_job (prop1, prop2, ...) values (@prop1, @prop2, ...)
            var propertiesString = string.Join(",\n\t", properties.Select(x => x.Name.ToLower() == "end" ? "\"end\"" : x.Name.ToLower()));
            var propertyParametersString = string.Join(",\n\t", properties.Select(x => $"@{x.Name.ToLower()}"));

            SQL_CreateJob = $"insert into sosync_job (\n\t{propertiesString}\n) values (\n\t{propertyParametersString}\n);\nSELECT currval(pg_get_serial_sequence('sosync_job','id')) id;\n";

            // Update statement
            //   update sosync_job set prop1 = @prop1, prop2 = @prop2, ... where id = @id
            SQL_UpdateJob = $"update sosync_job set\n\t{string.Join(",\n\t", properties.Select(x => x.Name.ToLower() == "end" ? "\"end\" = @end" : $"{x.Name.ToLower()} = @{x.Name.ToLower()}"))}\nwhere id = @id;";
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
            _config = config;

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
        public IDbTransaction BeginTransaction()
        {
            return _con.BeginTransaction();
        }

        public int ClosePreviousJobs(SyncJob closingSourceJob)
        {
            var parameters = new
            {
                job_source_sosync_write_date = closingSourceJob.Job_Source_Sosync_Write_Date.Value,
                job_source_system = closingSourceJob.Job_Source_System,
                job_source_model = closingSourceJob.Job_Source_Model,
                job_source_record_id = closingSourceJob.Job_Source_Record_ID,
                job_closed_by_job_id = closingSourceJob.ID,
                write_date = DateTime.UtcNow,
                job_log = $"Skipped due to sosync_job.id = {closingSourceJob.ID}."
            };

            var updated_rows = _con.ExecuteScalar<int>(
                Resources.ClosePreviousJobs_Update_SCRIPT,
                parameters
                );

            return updated_rows;
        }

        public void ReopenErrorJobs()
        {
            _con.Execute("update sosync_job set job_state = 'new' where job_state = 'error_retry';",
                commandTimeout: 60 * 2);
        }

        public int ArchiveFinishedSyncJobs()
        {
            try
            {
                return _con.Query<int>(Resources.Archive_finished_SyncJobs, commandTimeout: 10)
                    .SingleOrDefault();
            }
            catch (Exception)
            {
                var pids = _con.Query<int>(
                    "select pid from pg_stat_activity where datname = @db and application_name = 'sosync2' and query ilike 'with recursive to_be_moved%'",
                    new { db = _config.DB_Name })
                    .ToArray();

                if (pids.Length > 0)
                {
                    var endProcessesQuery = string.Join("\n", pids.Select(x => $"select pg_cancel_backend({x});"));
                    _con.Execute(endProcessesQuery);
                }

                throw;
            }
        }

        public async Task<int> ArchiveFinishedSyncJobsAsync()
        {
            try
            {
                return (await _con.QueryAsync<int>(Resources.Archive_finished_SyncJobs, commandTimeout: 15))
                    .SingleOrDefault();
            }
            catch (Exception)
            {

                throw;
            }
        }

        /// <summary>
        /// Returns the first unfinished parent job from the sync table and all its children
        /// in the hierarchy as a flat list.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<SyncJob> GetFirstOpenJobHierarchy(int limit)
        {
            var query = Resources.GetFirstOpenSynJobAndChildren_SELECT
                .Replace("%LIMIT%", limit.ToString("0"));

            var result = _con.Query<SyncJob>(
                query,
                commandTimeout: _cmdTimeoutSec);

            foreach (var r in result)
                CleanModel(r);

            return result;
        }

        /// <summary>
        /// Get the specified job by its ID.
        /// </summary>
        /// <param name="id">ID of the job to fetch.</param>
        /// <returns></returns>
        public SyncJob GetJob(int id)
        {
            var result = _con.Query<SyncJob>("select * from sosync_job where id = @id", new { id }, commandTimeout: _cmdTimeoutSec).SingleOrDefault();
            CleanModel(result);
            return result;
        }

        public SyncJob GetJobBy(int parentJobId, string jobSourceSystem, string jobSourceModel, int jobSourceRecordId)
        {
            var result = _con.Query<SyncJob>(
                "select * from sosync_job where parent_job_id = @parent_job_id and job_source_system = @job_source_system and job_source_model = @job_source_model and job_source_record_id = @job_source_record_id",
                new { parent_job_id = parentJobId, job_source_system = jobSourceSystem, job_source_model = jobSourceModel, job_source_record_id = jobSourceRecordId },
                commandTimeout: _cmdTimeoutSec)
                .SingleOrDefault();

            CleanModel(result);

            return result;
        }

        private void CleanModel(SyncJob model)
        {
            if (model == null)
                return;

            foreach (var prop in model.GetType().GetProperties())
            {
                if (prop.PropertyType == typeof(DateTime))
                {
                    // Set UTC flag on any date field of the sync job
                    prop.SetValue(model, DateTime.SpecifyKind((DateTime)prop.GetValue(model), DateTimeKind.Utc));
                }
                else if (prop.PropertyType == typeof(DateTime?))
                {
                    // Set UTC flag on any date field of the sync job
                    if (((DateTime?)prop.GetValue(model)).HasValue)
                        prop.SetValue(model, DateTime.SpecifyKind(((DateTime?)prop.GetValue(model)).Value, DateTimeKind.Utc));
                }
                else if (prop.PropertyType == typeof(string))
                {
                    // Override empty strings with null values
                    var value = (string)prop.GetValue(model);

                    if (string.IsNullOrEmpty(value))
                        prop.SetValue(model, null);
                }
            }
        }

        /// <summary>
        /// Creates a new sync job in the database.
        /// </summary>
        /// <param name="job">The sync job to be created.</param>
        public void CreateJob(SyncJob job, IDbTransaction transaction = null)
        {
            CleanModel(job);

            // The insert statement is dynamically created as a static value in the class initializer,
            // hence it is not read from resources
            var queryParams = new DynamicParameters(job);

            var inserted = _con.Query<JobInsertedInfo>(SQL_CreateJob, queryParams, transaction).Single();
            job.ID = inserted.ID;
        }

        public void CreateJobBulk(IEnumerable<SyncJob> jobs)
        {
            // TODO: Create real bulk insert
            foreach(var job in jobs)
            {
                CreateJob(job);
            }
        }

        /// <summary>
        /// Updates the sync in the database.
        /// </summary>
        /// <param name="job">The sync job to be updated.</param>
        public void UpdateJob(SyncJob job)
        {
            CleanModel(job);
            _con.Execute(DataService.SQL_UpdateJob, job, commandTimeout: _cmdTimeoutSec);
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