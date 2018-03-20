﻿using Npgsql;
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

namespace WebSosync.Data
{
    public class DataService : IDisposable
    {
        #region Members
        private static string SQL_CreateJob;
        private static string SQL_UpdateJob;

        private NpgsqlConnection _con;
        private int _cmdTimeoutSec = 10;
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
            SQL_CreateJob = $"insert into sync_table (\n\t{string.Join(",\n\t", properties.Select(x => x.Name.ToLower() == "end" ? "\"end\"" : x.Name.ToLower()))}\n) values (\n\t{string.Join(",\n\t", properties.Select(x => $"@{x.Name.ToLower()}"))}\n);\nSELECT currval(pg_get_serial_sequence('sync_table','job_id'));";

            // Update statement
            //   update sync_table set prop1 = @prop1, prop2 = @prop2, ... where job_id = @job_id
            SQL_UpdateJob = $"update sync_table set\n\t{string.Join(",\n\t", properties.Select(x => x.Name.ToLower() == "end" ? "\"end\" = @end" : $"{x.Name.ToLower()} = @{x.Name.ToLower()}"))}\nwhere job_id = @job_id;";
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
            // Initial table creation, new fields will only be added below over time
            _con.Execute(Resources.ResourceManager.GetString(ResourceNames.SetupDatabaseScript), commandTimeout: _cmdTimeoutSec);

            try
            {
                _con.Execute(Resources.ResourceManager.GetString(ResourceNames.CreateIndexScript), commandTimeout: 60);
            }
            catch (NpgsqlException ex)
            {
                // If the exception happened because the index already exists, ignore it.
                // Rethrow any other exception
                if (ex.ErrorCode != -2147467259)
                    throw ex;
            }

            // To modify the sync table in production
            // AddColumnIfNotExists("col_name", "col_type");
            // DropColumnIfExists("col_name");

            // New fields for additional logging & optimizations
            AddColumnIfNotExists("job_source_sosync_write_date", "timestamp without time zone");
            AddColumnIfNotExists("job_source_fields", "text");

            // New FK to reference the job responsible for closing a job
            AddColumnIfNotExists("job_closed_by_job_id", "integer");

            // New fields for job sync to Odoo
            AddColumnIfNotExists("job_to_fso_can_sync", "boolean");
            AddColumnIfNotExists("job_to_fso_sync_date", "timestamp without time zone");
            AddColumnIfNotExists("job_to_fso_sync_version", "timestamp without time zone");

            // New fields for doublet & deletion handling
            AddColumnIfNotExists("job_source_type", "text");
            AddColumnIfNotExists("job_source_target_record_id", "integer");
            AddColumnIfNotExists("job_source_merge_into_record_id", "integer");
            AddColumnIfNotExists("job_source_target_merge_into_record_id", "integer");
            AddColumnIfNotExists("sync_source_merge_into_record_id", "integer");
            AddColumnIfNotExists("sync_target_merge_into_record_id", "integer");

            CreateIndexIfNotExists(Resources.ResourceManager.GetString(ResourceNames.SyncJobToSyncIndex));
            CreateIndexIfNotExists(Resources.ResourceManager.GetString(ResourceNames.SyncJobToSyncIndex2));
        }

        private void CreateIndexIfNotExists(string ddlQuery)
        {
            try
            {
                _con.Execute(ddlQuery, commandTimeout: 60);
            }
            catch (NpgsqlException ex)
            {
                // If the exception happened because the index already exists, ignore it.
                // Rethrow any other exception
                if (ex.ErrorCode != -2147467259)
                    throw ex;
            }
        }

        /// <summary>
        /// Used for database setup, in case new columns are needed in production.
        /// </summary>
        /// <param name="column">The column name.</param>
        /// <param name="dataType">The pgSQL data type.</param>
        private void AddColumnIfNotExists(string column, string dataType)
        {
            _con.Execute(String.Format(Resources.ResourceManager.GetString(ResourceNames.SetupAddColumnScript), "sync_table", column, dataType), commandTimeout: _cmdTimeoutSec);
        }

        /// <summary>
        /// Used for database setup, in case a column needs to be dropped from production
        /// </summary>
        /// <param name="column">The column name.</param>
        private void DropColumnIfExists(string column)
        {
            _con.Execute(String.Format(Resources.ResourceManager.GetString(ResourceNames.SetupDropColumnScript), "sync_table", column), commandTimeout: _cmdTimeoutSec);
        }

        /// <summary>
        /// Drops the sync_table. Never do this in production!
        /// </summary>
        private void DestroySosyncDatabase()
        {
            _con.Execute("drop table sync_table;", commandTimeout: _cmdTimeoutSec);
        }

        /// <summary>
        /// Reads all open SyncJobs from the database.
        /// </summary>
        /// <returns></returns>
        public List<SyncJob> GetJobs(bool onlyOpenJobs)
        {
            if (onlyOpenJobs)
            {
                var result = _con.Query<SyncJob>(
                    Resources.ResourceManager.GetString(ResourceNames.GetAllOpenSyncJobsSelect),
                    commandTimeout: _cmdTimeoutSec)
                    .AsList();

                foreach (var r in result)
                    CleanModel(r);

                return result;
            }
            else
            {
                var result = _con.Query<SyncJob>(
                    "select * from sync_table",
                    commandTimeout: _cmdTimeoutSec)
                    .AsList();

                foreach (var r in result)
                    CleanModel(r);

                return result;
            }
        }

        public int ClosePreviousJobs(SyncJob closingSourceJob)
        {
            var parameters = new
            {
                job_source_sosync_write_date = closingSourceJob.Job_Source_Sosync_Write_Date.Value,
                job_source_system = closingSourceJob.Job_Source_System,
                job_source_model = closingSourceJob.Job_Source_Model,
                job_source_record_id = closingSourceJob.Job_Source_Record_ID,
                job_closed_by_job_id = closingSourceJob.Job_ID,
                job_last_change = DateTime.UtcNow,
                job_log = $"Skipped due to job_id = {closingSourceJob.Job_ID}."
            };

            var updated_rows = _con.ExecuteScalar<int>(
                Resources.ResourceManager.GetString(ResourceNames.ClosePreviousJobsUpdateScript),
                parameters
                );

            return updated_rows;
        }

        /// <summary>
        /// Returns the first unfinished parent job from the sync table and all its children
        /// in the hierarchy as a flat list.
        /// </summary>
        /// <returns></returns>
        public List<SyncJob> GetFirstOpenJobHierarchy()
        {
            var result = _con.Query<SyncJob>(
                Resources.ResourceManager.GetString(ResourceNames.GetFirstOpenSynJobAndChildren),
                commandTimeout: _cmdTimeoutSec)
                .AsList();

            foreach (var r in result)
                CleanModel(r);

            return result;
        }

        /// <summary>
        /// Returns the first job to be synced to fso.
        /// </summary>
        /// <returns></returns>
        public SyncJob GetJobToSync()
        {
            var result = _con.Query<SyncJob>(
                Resources.ResourceManager.GetString(ResourceNames.GetFirstSyncJobToSyncSelect),
                commandTimeout: _cmdTimeoutSec)
                .SingleOrDefault();

            CleanModel(result);

            return result;
        }

        /// <summary>
        /// Get the specified job by its ID.
        /// </summary>
        /// <param name="id">ID of the job to fetch.</param>
        /// <returns></returns>
        public SyncJob GetJob(int id)
        {
            var result = _con.Query<SyncJob>("select * from sync_table where job_id = @job_id", new { job_id = id }, commandTimeout: _cmdTimeoutSec).SingleOrDefault();
            CleanModel(result);
            return result;
        }

        public SyncJob GetJobBy(int parentJobId, string jobSourceSystem, string jobSourceModel, int jobSourceRecordId)
        {
            var result = _con.Query<SyncJob>(
                "select * from sync_table where parent_job_id = @parent_job_id and job_source_system = @job_source_system and job_source_model = @job_source_model and job_source_record_id = @job_source_record_id",
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
        public void CreateJob(SyncJob job)
        {
            CleanModel(job);

            // The insert statement is dynamically created as a static value in the class initializer,
            // hence it is not read from resources
            job.Job_ID = _con.Query<int>(DataService.SQL_CreateJob, job).Single();
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

        /// <summary>
        /// Updates only the specified field of the job in the database.
        /// </summary>
        /// <param name="job">The job to be updated.</param>
        /// <param name="propertySelector">The member expression for which field should be updated in the database.</param>
        public void UpdateJob<TProp>(SyncJob job, Expression<Func<SyncJob, TProp>> propertySelector)
        {
            CleanModel(job);

            var tblAtt = job.GetType().GetTypeInfo().GetCustomAttribute<DataContractAttribute>();
            var prop = ((PropertyInfo)((MemberExpression)propertySelector.Body).Member);
            var propAtt = prop.GetCustomAttribute<DataMemberAttribute>();

            var tblName = tblAtt == null ? typeof(SyncJob).Name : tblAtt.Name;
            var propName = propAtt == null ? prop.Name : propAtt.Name;

            _con.Execute($"update {tblName} set {propName} = @{propName} where job_id = @job_id", job, commandTimeout: _cmdTimeoutSec);
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
