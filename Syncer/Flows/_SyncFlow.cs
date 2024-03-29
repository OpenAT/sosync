﻿using DaDi.Odoo;
using dadi_data.Interfaces;
using dadi_data.Models;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncer.Attributes;
using Syncer.Enumerations;
using Syncer.Exceptions;
using Syncer.Helpers;
using Syncer.Models;
using Syncer.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using WebSosync.Common;
using WebSosync.Data;
using WebSosync.Data.Constants;
using WebSosync.Data.Models;

namespace Syncer.Flows
{
    /// <summary>
    /// Base class for any sync flow.
    /// </summary>
    public abstract class SyncFlow : IDisposable
    {
        #region Constants
        public const string MssqlTargetSuccessMessage = "success";
        #endregion

        #region Members
        private List<ChildJobRequest> _requiredChildJobs;
        private List<ChildJobRequest> _requiredPostChildJobs;
        #endregion

        #region Properties
        protected SyncServiceCollection Svc { get; }

        protected IReadOnlyList<ChildJobRequest> RequiredChildJobs { get { return _requiredChildJobs.AsReadOnly(); } }
        protected IReadOnlyList<ChildJobRequest> RequiredPostTransformChildJobs { get { return _requiredPostChildJobs.AsReadOnly(); } }

        public bool SkipAutoSyncSource { get; set; }

        private StringBuilder _timeLog;

        protected SyncJob Job { get; set; }

        public CancellationToken CancelToken { get; set; }

        public string StudioModelName { get; set; }
        public string OnlineModelName { get; set; }
        public bool IsStudioMultiModel { get; set; }

        public bool ConcurrencyStudioWins { get; set; }
        #endregion

        #region Constructors
        public SyncFlow(SyncServiceCollection svc)
        {
            Svc = svc;

            SkipAutoSyncSource = false;
            _timeLog = new StringBuilder();

            _requiredChildJobs = new List<ChildJobRequest>();
            _requiredPostChildJobs = new List<ChildJobRequest>();

            ConcurrencyStudioWins = true;
            SetModelPropertiesFromAttributes();
        }
        #endregion

        #region Methods
        protected DateTime? Treat2000DateAsNull(DateTime? mssqlDate)
        {
            if (mssqlDate == new DateTime(2000, 1, 1))
                return null;

            return mssqlDate;
        }

        public void LogMilliseconds(string operation, double ms)
        {
            _timeLog.AppendLine($"{operation}: {ms.ToString("0")} ms");
        }

        public string GetTimeLog()
        {
            return _timeLog.ToString();
        }

        protected DataService GetDb()
        {
            return new DataService(Svc.Config);
        }

        private void SetModelPropertiesFromAttributes()
        {
            var studioAtt = GetType().GetCustomAttribute<StudioModelAttribute>();
            var onlineAtt = GetType().GetCustomAttribute<OnlineModelAttribute>();
            var studioMultiAtt = GetType().GetCustomAttribute<StudioMultiModelAttribute>();
            var concurrencyOnlineWinsAtt = GetType().GetCustomAttribute<ConcurrencyOnlineWinsAttribute>();

            if (studioAtt != null)
                StudioModelName = studioAtt.Name;

            if (onlineAtt != null)
                OnlineModelName = onlineAtt.Name;

            if (studioMultiAtt != null)
                IsStudioMultiModel = true;

            if (concurrencyOnlineWinsAtt != null)
                ConcurrencyStudioWins = false;
        }

        /// <summary>
        /// Get IDs and write date for the model in online.
        /// </summary>
        /// <param name="onlineID">The ID for the model.</param>
        /// <returns></returns>
        protected virtual ModelInfo GetOnlineInfo(int onlineID)
        {
            return GetDefaultOnlineModelInfo(onlineID, OnlineModelName);
        }

        /// <summary>
        /// Get the specified foreign key for an online model via FSOnline.
        /// </summary>
        /// <param name="onlineModelName">Model name in Odoo</param>
        /// <param name="id">Identity in Odoo</param>
        /// <param name="onlineReferenceField">Field name to return</param>
        /// <returns></returns>
        protected int? GetOnlineReferenceID(string onlineModelName, int id, string onlineReferenceField)
        {
            var odooDict = Svc.OdooService.Client.GetDictionary(
                onlineModelName,
                id,
                new[] { onlineReferenceField });

            if (!odooDict.ContainsKey(onlineReferenceField))
                return null;

            var result = (int?)null;

            if (odooDict[onlineReferenceField] is List<Object>)
                result = int.Parse((string)((List<Object>)odooDict[onlineReferenceField])[0]);
            else
                result = int.Parse((string)odooDict[onlineReferenceField]);

            return result > 0 ? result : (int?)null;
        }

        public int? GetStudioIDFromOnlineReference<T>(
            string studioModelName,
            T onlineModel,
            Expression<Func<T, object[]>> expression,
            bool required)
        {
            var memberExpression = expression.Body as MemberExpression;

            if (memberExpression == null)
                throw new ArgumentException($"{nameof(expression)} is expected to be a property expression.");

            int? result = null;
            var value = (object[])onlineModel.GetType().GetProperty(memberExpression.Member.Name).GetValue(onlineModel);

            if (value != null && value.Length > 1)
            {
                var odooID = Convert.ToInt32(value[0]);

                result = GetStudioIDFromMssqlViaOnlineID(
                    studioModelName,
                    Svc.MdbService.GetStudioModelIdentity(studioModelName),
                    odooID);

                if (odooID > 0 && !result.HasValue)
                    throw new SyncerException($"{nameof(GetStudioIDFromOnlineReference)}: {studioModelName} not found via sosync_fso_id = {odooID}.");
            }

            if (required && result == null)
                throw new MissingForeignKeyException($"Property {onlineModel.GetType().Name}.{memberExpression.Member.Name} is null, but was specified required.");

            return result;
        }

        public int? GetStudioIDFromOnlineReference<T>(
            string studioModelName,
            T onlineModel,
            Expression<Func<T, int?>> expression,
            bool required)
        {
            var memberExpression = expression.Body as MemberExpression;

            if (memberExpression == null)
                throw new ArgumentException($"{nameof(expression)} is expected to be a property expression.");

            int? result = null;
            var value = (int?)onlineModel.GetType().GetProperty(memberExpression.Member.Name).GetValue(onlineModel);

            if (value != null && value.Value > 0)
            {
                var odooID = Convert.ToInt32(value);

                result = GetStudioIDFromMssqlViaOnlineID(
                    studioModelName,
                    Svc.MdbService.GetStudioModelIdentity(studioModelName),
                    odooID);

                if (odooID > 0 && !result.HasValue)
                    throw new SyncerException($"{nameof(GetStudioIDFromOnlineReference)}: {studioModelName} not found via sosync_fso_id = {odooID}.");
            }

            if (required && result == null)
                throw new MissingForeignKeyException($"Property {onlineModel.GetType().Name}.{memberExpression.Member.Name} is null, but was specified required.");

            return result;
        }

        protected int? GetCountryIdForLandId(int? landID)
        {
            string isoCode = Svc.MdbService.GetIsoCodeForLandID(landID);
            return Svc.OdooService.GetCountryIDForIsoCode(isoCode);
        }

        protected int? GetLandIdForCountryId(int? countryID)
        {
            string isoCode = Svc.OdooService.GetIsoCodeForCountryID(countryID);
            return Svc.MdbService.GetLandIDForIsoCode(isoCode);
        }

        /// <summary>
        /// Get IDs and write date for the model in studio.
        /// </summary>
        /// <param name="studioID">The Studio ID for the model.</param>
        /// <returns></returns>
        protected abstract ModelInfo GetStudioInfo(int studioID);

        /// <summary>
        /// Configure the flow for the sync direction online to studio.
        /// This configuration can at some point be read from meta data
        /// from fs online.
        /// </summary>
        /// <param name="onlineID">The Online ID for the model.</param>
        protected virtual void SetupOnlineToStudioChildJobs(int onlineID)
        {

        }

        /// <summary>
        /// Configure the flow for the sync direction studio to online.
        /// </summary>
        /// <param name="studioID">The Studio ID for the model.</param>
        protected virtual void SetupStudioToOnlineChildJobs(int studioID)
        {

        }

        /// <summary>
        /// Read the studio model with the given ID and transform it
        /// to an online model. Ensure transactional behaviour.
        /// </summary>
        /// <param name="studioID">The Studio ID for the source.</param>
        protected abstract void TransformToOnline(int studioID, TransformType action);

        /// <summary>
        /// Read the online model with the given ID and transform it
        /// to a studio model. Ensure transactional behaviour.
        /// </summary>
        /// <param name="onlineID">The Online ID for the source.</param>
        protected abstract void TransformToStudio(int onlineID, TransformType action);

        /// <summary>
        /// Get the ID, sosync_write_date and write_date for a fso model.
        /// </summary>
        /// <param name="id">The fso ID of the model.</param>
        /// <param name="model">The model name.</param>
        /// <returns></returns>
        private static Dictionary<string, Tuple<double, int>> _DebugOnlineStat = new Dictionary<string, Tuple<double, int>>();
        protected ModelInfo GetDefaultOnlineModelInfo(int id, string model)
        {
            var s = new Stopwatch();
            s.Start();

            var dicModel = Svc.OdooService.Client.GetDictionary(
                model,
                id,
                new string[] { "id", "sosync_fs_id", "write_date", "sosync_write_date", "sosync_synced_version" });

            s.Stop();
            lock (_DebugOnlineStat)
            {
                if (!_DebugOnlineStat.ContainsKey(model))
                    _DebugOnlineStat.Add(model, new Tuple<double, int>(0, 0));

                var t = _DebugOnlineStat[model];
                _DebugOnlineStat[model] = new Tuple<double, int>(t.Item1 + s.Elapsed.TotalMilliseconds, t.Item2 + 1);
            }

            if (!Svc.OdooService.Client.IsValidResult(dicModel))
                throw new ModelNotFoundException(SosyncSystem.FSOnline, model, id);

            var fsID = OdooConvert.ToInt32((string)dicModel["sosync_fs_id"]);

            if (fsID == 0)
                fsID = null;

            var sosyncWriteDate = OdooConvert.ToDateTime((string)dicModel["sosync_write_date"], true);
            var sosyncSyncedVersion = OdooConvert.ToDateTime((string)dicModel["sosync_synced_version"], true);
            var writeDate = OdooConvert.ToDateTime((string)dicModel["write_date"], true);

            return new ModelInfo(id, fsID, sosyncWriteDate, writeDate, sosyncSyncedVersion);
        }

        private static Dictionary<string, Tuple<double, int>> _DebugStudioStat = new Dictionary<string, Tuple<double, int>>();
        protected ModelInfo GetDefaultStudioModelInfo<TStudio>(int studioID)
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            using (var db = Svc.MdbService.GetDataService<TStudio>())
            {
                var s = new Stopwatch();
                s.Start();

                var studioModel = db.Read(
                    $"select write_date, sosync_write_date, sosync_fso_id, last_sync_version from {Svc.MdbService.GetStudioModelReadView(StudioModelName)} where {Svc.MdbService.GetStudioModelIdentity(StudioModelName)} = @ID",
                    new { ID = studioID })
                    .SingleOrDefault();

                s.Stop();
                lock (_DebugStudioStat)
                {
                    if (!_DebugStudioStat.ContainsKey(typeof(TStudio).Name))
                        _DebugStudioStat.Add(typeof(TStudio).Name, new Tuple<double, int>(0, 0));

                    var t = _DebugStudioStat[typeof(TStudio).Name];
                    _DebugStudioStat[typeof(TStudio).Name] = new Tuple<double, int>(t.Item1 + s.Elapsed.TotalMilliseconds, t.Item2 + 1);
                }

                if (studioModel != null)
                {
                    return new ModelInfo(
                        studioID,
                        studioModel.sosync_fso_id,
                        studioModel.sosync_write_date,
                        studioModel.write_date,
                        studioModel.last_sync_version);
                }
            }

            return null;
        }

        protected int? GetOnlineIDFromOdooViaStudioID(string onlineModelName, int fsId)
        {
            var odooID = 0;

            Svc.Log.LogInformation($"Going to SearchByField, inside {nameof(GetOnlineIDFromOdooViaStudioID)} ({nameof(onlineModelName)}={onlineModelName}, {nameof(fsId)}={fsId})");

            var results = Svc.OdooService.Client.SearchByField(onlineModelName, "sosync_fs_id", "=", fsId).ToList();

            Svc.Log.LogInformation($"SearchByField returned, inside {nameof(GetOnlineIDFromOdooViaStudioID)} ({nameof(onlineModelName)}={onlineModelName}, {nameof(fsId)}={fsId})");

            if (results.Count == 1)
                odooID = results[0];
            else if (results.Count > 1)
                throw new SyncerException($"{nameof(GetOnlineIDFromOdooViaStudioID)}(): Multiple Odoo-IDs found.");

            if (odooID > 0)
                return odooID;

            return null;
        }

        protected int? GetStudioIDFromMssqlViaOnlineID(string studioModelName, string idName, int onlineID)
        {
            // Since we're only running a simple query, the DataService type doesn't matter
            try
            {
                using (var db = Svc.MdbService.GetDataService<dboPerson>())
                {
                    var foundStudioID = db.ExecuteQuery<int?>(
                        $"select {idName} from {studioModelName} where sosync_fso_id = @fso_id",
                        new { fso_id = onlineID })
                        .SingleOrDefault();

                    return foundStudioID;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected bool IsValidFsID(int? sosync_fs_id)
        {
            return sosync_fs_id.HasValue && sosync_fs_id.Value > 0;
        }

        
        // These error types are instantly retried instead of going to error-retry immediately
        private static Type[] RetryExceptionTypes = new[]
        {
            typeof(Win32Exception),
            typeof(ConsistencyException)
        };

        /// <summary>
        /// Recursively checks and exception and its inner exceptions
        /// for matching exception types. Returns true, if any exception
        /// matches any of the specified types.
        /// </summary>
        /// <param name="ex">The exception to check.</param>
        /// <param name="types">The exception types to compare against.</param>
        /// <returns>True, if any exception matches any of the types.</returns>
        private bool AnyExceptionOfType(Exception ex, Type[] types)
        {
            if (ex == null)
            {
                return false;
            }    

            if (AnyExceptionOfType(ex.InnerException, types))
            {
                return true;
            }

            if (types.Contains(ex.GetType()))
            {
                return true;
            }

            return false;
        }

        public void Start(FlowService flowService, SyncJob job, DateTime loadTimeUTC, ref bool requireRestart, ref string restartReason)
        {
            if (job == null)
                throw new SyncerException($"Parameter '{nameof(job)}' cannot be null.");

            Job = job;

            try
            {
                var done = false;
                var maxErrorRetries = 3;
                var errorRetries = maxErrorRetries;

                while (!done)
                {
                    try
                    {
                        StartFlow(flowService, loadTimeUTC, ref requireRestart, ref restartReason);
                        done = true;
                    }
                    catch (Exception ex)
                    {
                        if (AnyExceptionOfType(ex, RetryExceptionTypes))
                        {
                            // Specific errors lead to a retry after a short delay
                            errorRetries--;

                            var usedTries = (maxErrorRetries - errorRetries);
                            var pause = (int)Math.Round(100 * Math.Pow(usedTries, 3), 0);

                            Svc.Log.LogWarning($"Intermittent error, job_id {job.ID} ({job.Job_Source_Model} {job.Job_Source_Record_ID}), sleeping {pause} ms and retrying.");
                            Thread.Sleep(pause);

                            if (errorRetries <= 0)
                            {
                                // Out of retries, rethrow
                                throw;
                            }
                        }
                        else
                        {
                            // Any other error, rethrow
                            throw;
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Svc.Log.LogError(ex.ToString());
                UpdateJobError(SosyncError.Unknown, $"{ex}\nProcedure: {ex.Procedure}");
            }
            catch (ModelNotFoundException ex)
            {
                // Do not retry model not found errors
                Svc.Log.LogError(ex.ToString());
                UpdateJobError(SosyncError.Unknown, ex.ToString(), useErrorRetry: false);
            }
            catch (JobDateMismatchException ex)
            {
                // Do not retry job date errors
                Svc.Log.LogError(ex.ToString());
                UpdateJobError(SosyncError.Unknown, ex.ToString(), useErrorRetry: false);
            }
            catch (ChildJobException ex)
            {
                UpdateJobError(SosyncError.ChildJob, ex.ToString(), useErrorRetry: true);
            }
            catch (TransformationException ex)
            {
                UpdateJobError(SosyncError.Transformation, ex.ToString(), useErrorRetry: true);
            }
            catch (SyncCleanupException ex)
            {
                UpdateJobError(SosyncError.Cleanup, ex.ToString(), useErrorRetry: true);
            }
            catch (Exception ex)
            {
                Svc.Log.LogError(ex.ToString());
                UpdateJobError(SosyncError.Unknown, ex.ToString());
            }
            finally
            {
                Job.Job_Log += $"{(string.IsNullOrEmpty(job.Job_Log) ? "" : "\n\n")}{GetTimeLog()}\n";
                UpdateJob(Job, "Saving job log after finished flow");
            }
        }

        protected abstract void StartFlow(FlowService flowService, DateTime loadTimeUTC, ref bool requireRestart, ref string restartReason);

        public void SetupChildJobRequests()
        {
            var s = Stopwatch.StartNew();
            LogMs(0, $"\n{nameof(SetupChildJobRequests)} start", Job.ID, 0);

            if (Job.Sync_Source_System == SosyncSystem.FSOnline.Value)
                SetupOnlineToStudioChildJobs(Job.Sync_Source_Record_ID.Value);
            else
                SetupStudioToOnlineChildJobs(Job.Sync_Source_Record_ID.Value);

            s.Stop();
            LogMs(0, $"{nameof(SetupChildJobRequests)} end", Job.ID, s.ElapsedMilliseconds);
        }

        protected void HandleChildJobs(
            string childDescription,
            IEnumerable<ChildJobRequest> requestedChildJobs,
            IEnumerable<SyncJob> existingChildren,
            FlowService flowService,
            DateTime? initialWriteDate,
            Stopwatch consistencyWatch,
            ref bool requireRestart,
            ref string restartReason)
        {
            LogMs(0, $"\n{nameof(HandleChildJobs)} start", Job.ID, 0);

            // The derived sync flows can use RequestChildJob() method to
            // fill _requiredChildJobs.

            var s = new Stopwatch();
            s.Start();

            if (requestedChildJobs.Count() == 0 && Job.Children.Count == 0)
            {
                s.Stop();
                LogMs(0, "ChildJobs", Job.ID, s.ElapsedMilliseconds);

                return;
            }

            try
            {
                // Process the requested child jobs:
                // 1) Ignore finished child jobs
                // 2) If child job is open --> Skipped?
                // 3) Create all requested child jobs
                try
                {
                    // try-finally to ensure the child_job_end date is written.
                    // Actual errors should still bubble up, NOT be caught here.
                    UpdateJobChildStart(Job);

                    var allChildJobsFinished = true;

                    // Child jobs pre-existing from database
                    if (existingChildren != null)
                    {
                        foreach (var childJob in existingChildren)
                        {
                            if (childJob.Job_State == SosyncState.Error.Value || childJob.Job_State == SosyncState.ErrorRetry.Value)
                                throw new SyncerException($"{childDescription} ({childJob.ID}) for [{childJob.Job_Source_System}] {childJob.Job_Source_Model} ({childJob.Job_Source_Record_ID}) failed.");

                            if (childJob.Job_State == SosyncState.New.Value
                                || childJob.Job_State == SosyncState.InProgress.Value)
                            {
                                UpdateJobStart(childJob, DateTime.UtcNow);

                                var constructorParams = new object[] { Svc };
                                using (SyncFlow flow = (SyncFlow)Activator.CreateInstance(Svc.FlowService.GetFlow(childJob.Job_Source_Type, childJob.Job_Source_Model), constructorParams))
                                {
                                    flow.Start(flowService, childJob, DateTime.UtcNow, ref requireRestart, ref restartReason);
                                }
                            }

                            if (childJob.Job_State == SosyncState.Done.Value
                                || childJob.Job_State == SosyncState.Skipped.Value)
                                allChildJobsFinished &= true;
                            else
                                allChildJobsFinished &= false;
                        }
                    }

                    // Child jobs from model requests
                    foreach (ChildJobRequest request in requestedChildJobs)
                    {
                        if (initialWriteDate.HasValue && !IsConsistent(Job, initialWriteDate, consistencyWatch))
                        {
                            // Job is inconsistent, cancel the flow and leave it "in progress",
                            // so it will be restarted later.
                            UpdateJobInconsistent(Job, 1);
                            requireRestart = true;
                            restartReason = "Consistency check 1";
                            throw new ConsistencyException(restartReason);
                        }

#warning TODO: bubble up the hierarchy to check for circular references
                        // 1) Check if any parent job already has
                        //    sync_source_system, sync_source_model, sync_source_record-id

                        // 2 Create & Process child job
                        var childJob = new SyncJob()
                        {
                            Parent_Job_ID = Job.ID,
                            Job_State = SosyncState.New.Value,
                            Job_Date = DateTime.UtcNow,
                            Job_Fetched = DateTime.UtcNow,
                            Job_Source_System = request.JobSourceSystem,
                            Job_Source_Model = request.JobSourceModel,
                            Job_Source_Record_ID = request.JobSourceRecordID,
                            Job_Priority = Svc.FlowService.GetModelPriority(request.JobSourceModel),
                            Write_Date = DateTime.UtcNow,
                            Job_Source_Type = request.JobSourceType.Value,
                            Create_Date = DateTime.UtcNow
                        };

                        if (request.ForceDirection)
                        {
                            childJob.Sync_Source_System = request.JobSourceSystem;
                            childJob.Sync_Source_Model = request.JobSourceModel;
                            childJob.Sync_Source_Record_ID = request.JobSourceRecordID;

                            childJob.Sync_Target_System =
                                Job.Job_Source_System == SosyncSystem.FundraisingStudio.Value
                                ? SosyncSystem.FSOnline.Value
                                : SosyncSystem.FundraisingStudio.Value;

                            childJob.Sync_Target_Model =
                                Job.Job_Source_System == SosyncSystem.FundraisingStudio.Value
                                ? OnlineModelName
                                : StudioModelName;

                            childJob.Sync_Target_Record_ID =
                                Job.Job_Source_System == SosyncSystem.FundraisingStudio.Value
                                ? GetOnlineIDFromOdooViaStudioID(OnlineModelName, request.JobSourceRecordID)
                                : GetStudioIDFromMssqlViaOnlineID(StudioModelName, Svc.MdbService.GetStudioModelIdentity(StudioModelName), request.JobSourceRecordID);

                            childJob.Job_Log += $"Forcing direction: {request.JobSourceModel} ({request.JobSourceRecordID}) --> {childJob.Sync_Target_Model} ({childJob.Sync_Target_Record_ID})\n";
                        }

                        // Try to fetch a child job with the same main properties
                        SyncJob entry = null;
                        using (var db = GetDb())
                        {
                            entry = db.GetJobBy(Job.ID, childJob.Job_Source_System, childJob.Job_Source_Model, childJob.Job_Source_Record_ID);

                            // If the child job wasn't created yet, create it
                            if (entry == null)
                            {
                                Svc.Log.LogInformation($"Creating {childDescription} for job ({Job.ID}) for [{childJob.Job_Source_System}] {childJob.Job_Source_Model} ({childJob.Job_Source_Record_ID}).");
                                db.CreateJob(childJob);
                                Job.Children.Add(childJob);

                                entry = childJob;
                            }
                        }

                        if (entry.Job_State == SosyncState.Error.Value || entry.Job_State == SosyncState.ErrorRetry.Value)
                            throw new SyncerException($"{childDescription} ({entry.ID}) for [{entry.Job_Source_System}] {entry.Job_Source_Model} ({entry.Job_Source_Record_ID}) failed.");

                        // If the job is new or marked as "in progress", run it
                        if (entry.Job_State == SosyncState.New.Value
                            || entry.Job_State == SosyncState.InProgress.Value)
                        {
                            Svc.Log.LogInformation($"Executing {childDescription} ({entry.ID})");

                            UpdateJobStart(entry, DateTime.UtcNow);

                            // SyncFlow flow = (SyncFlow)Service.GetService(flowService.GetFlow(entry.Job_Source_Type, entry.Job_Source_Model));

                            // Get the flow for the job source model, and start it
                            var constructorParams = new object[] { Svc };
                            using (SyncFlow flow = (SyncFlow)Activator.CreateInstance(Svc.FlowService.GetFlow(entry.Job_Source_Type, entry.Job_Source_Model), constructorParams))
                            {
                                if (request.ForceDirection)
                                    flow.SkipAutoSyncSource = true;

                                flow.Start(flowService, entry, DateTime.UtcNow, ref requireRestart, ref restartReason);

                                if (childJob.Job_State == SosyncState.Done.Value
                                    || childJob.Job_State == SosyncState.Skipped.Value)
                                    allChildJobsFinished &= true;
                                else
                                    allChildJobsFinished &= false;
                            }
                        }
                    }

                    //if (!allChildJobsFinished)
                    //    throw new SyncerException($"No child job errors occured, but there were still unfinished child jobs left.");

                    if (!allChildJobsFinished)
                    {
                        // Child jobs are not yet finished, despite just processing them.
                        // Require restart, and stop this job, but leave it open so it is
                        // processed again
                        UpdateJobInconsistent(Job, 2);
                        requireRestart = true;
                        restartReason = "Consistency check 2";
                        throw new ConsistencyException(restartReason);
                    }
                }
                finally
                {
                    // In any case, log the child end at this point
                    UpdateJobChildEnd();
                }
            }
            catch (ConsistencyException)
            {
                requireRestart = true;
            }
            catch (Exception ex)
            {
                UpdateJobError(SosyncError.ChildJob, ex.ToString());
                throw;
            }
            s.Stop();
            LogMs(0, $"ChildJobs ({childDescription})", Job.ID, s.ElapsedMilliseconds);
            s.Reset();
        }

        protected void HandleTransformation(string description, DateTime? initialWriteDate, Stopwatch consistencyWatch, ref bool requireRestart, ref string restartReason)
        {
            LogMs(0, $"\n{nameof(HandleTransformation)} start", Job.ID, 0);

            var s = new Stopwatch();
            s.Start();
            try
            {
                if (!IsConsistent(Job, initialWriteDate, consistencyWatch))
                {
                    // Job is inconsistent, cancel the current flow. Make sure
                    // To do this outside the try-finally block, because we want
                    // the job to stay "in progress".
                    UpdateJobInconsistent(Job, 3);
                    requireRestart = true;
                    restartReason = "Consistency check 3";
                    throw new ConsistencyException(restartReason);
                }

                try
                {
                    // try-finally to ensure the sync_end date is written.
                    // Actual errors should still bubble up, NOT be caught here.

                    UpdateJobSyncStart();

                    var action = Job.Sync_Target_Record_ID > 0 ? TransformType.Update : TransformType.CreateNew;

                    Svc.Log.LogInformation(description);

                    if (Job.Sync_Source_System == SosyncSystem.FSOnline.Value)
                        TransformToStudio(Job.Sync_Source_Record_ID.Value, action);
                    else
                        TransformToOnline(Job.Sync_Source_Record_ID.Value, action);
                }
                finally
                {
                    // In any case, log the transformation/sync end at this point
                    UpdateJobSyncEnd();
                }
            }
            catch (SqlException ex)
            {
                UpdateJobError(SosyncError.Transformation, $"4) Transformation/sync:\n{ex.ToString()}\nProcedure: {ex.Procedure}\n");
                throw;
            }
            catch (SyncerDeletionFailedException ex)
            {
                Svc.Log.LogWarning(ex.Message + " Assuming model already deleted. Skipping job.");
                Job.Job_Error_Code = SosyncError.Transformation.Value;
                Job.Job_Error_Text += ex.ToString();
                UpdateJobSkipped();
            }
            catch (AddressBlockNotUpToDateException ex)
            {
                Svc.Log.LogWarning(ex.Message);
                UpdateJobInconsistentBlock(Job);
                requireRestart = true;
                restartReason = "AddresseBlock not up to date, requesting restart.";
            }
            catch (ConsistencyException ex)
            {
                Svc.Log.LogWarning($"Job {Job.ID} threw a consistency exception: {ex.Message}");
                requireRestart = true;
                restartReason = ex.Message;
                throw;
            }
            catch (Exception ex)
            {
                UpdateJobError(SosyncError.Transformation, $"4) Transformation/sync:\n{ex.ToString()}");
                throw;
            }
            s.Stop();
            LogMs(0, $"{nameof(HandleTransformation)} done", Job.ID, s.ElapsedMilliseconds);
            s.Reset();
        }

        /// <summary>
        /// Inform the sync flow that a specific model is needed as a child job
        /// for the current flow.
        /// </summary>
        /// <param name="system">The job source system, use <see cref="SosyncSystem"/> constants.</param>
        /// <param name="model">The requested source model.</param>
        /// <param name="id">The requested source ID for the model.</param>
        /// <param name="jobSourceType">The requested job source type.</param>
        protected void RequestChildJob(SosyncSystem system, string model, int id, SosyncJobSourceType jobSourceType)
        {
            _requiredChildJobs.Add(new ChildJobRequest(system, model, id, jobSourceType, false));
        }

        /// <summary>
        /// Inform the sync flow that a specific model is needed as a child job
        /// after the transformation is finished.
        /// </summary>
        /// <param name="system">The job source system, use <see cref="SosyncSystem"/> constants.</param>
        /// <param name="model">The requested source model.</param>
        /// <param name="id">The requested source ID for the model.</param>
        /// <param name="jobSourceType">The requested job source type.</param>
        protected void RequestPostTransformChildJob(SosyncSystem system, string model, int id, bool forceDirection, SosyncJobSourceType jobSourceType)
        {
            _requiredPostChildJobs.Add(new ChildJobRequest(system, model, id, jobSourceType, forceDirection));
        }

        /// <summary>
        /// Uses MSSQL and Odoo to query the Online-ID for the given Studio-ID.
        /// Odoo is only queried as a backup, if MSSQL does not have an ID.
        /// </summary>
        /// <typeparam name="TStudio">Studio model type</typeparam>
        /// <param name="studioModelName">Studio model name</param>
        /// <param name="onlineModelName">Online model name</param>
        /// <param name="studioID">Studio-ID for searching the Online-ID</param>
        /// <returns>The Online-ID if found, otherwise null</returns>
        protected int? GetOnlineID<TStudio>(string studioModelName, string onlineModelName, int studioID)
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            TStudio studioModel = null;
            using (var db = Svc.MdbService.GetDataService<TStudio>())
            {
                studioModel = db.Read(
                    $"select sosync_fso_id from {studioModelName} where {Svc.MdbService.GetStudioModelIdentity(studioModelName)} = @ID",
                    new { ID = studioID })
                    .SingleOrDefault();

                if (!studioModel.sosync_fso_id.HasValue)
                    return GetOnlineIDFromOdooViaStudioID(onlineModelName, studioID);

                return studioModel.sosync_fso_id;
            }
        }

        /// <summary>
        /// Uses Odoo and MSSQL to query the Studio-ID for the given Online-ID.
        /// MSSQL is only queried as a backup, if Odoo does not have an ID.
        /// </summary>
        /// <typeparam name="TStudio">Studio model type</typeparam>
        /// <param name="onlineModelName">Online model name</param>
        /// <param name="studioModelName">Studio model name</param>
        /// <param name="onlineID">Online-ID for searching the Studio-ID</param>
        /// <returns>The Studio-ID if found, otherwise null</returns>
        protected int? GetStudioID<TStudio>(string onlineModelName, string studioModelName, int onlineID)
            where TStudio : MdbModelBase, ISosyncable, new()
        {
            int? studioID = null;

            var odooModel = Svc.OdooService.Client.GetDictionary(onlineModelName, onlineID, new[] { "sosync_fs_id" });

            if (odooModel.ContainsKey("sosync_fs_id"))
                studioID = OdooConvert.ToInt32((string)odooModel["sosync_fs_id"]);

            if (!studioID.HasValue || studioID == 0)
            {
                using (var db = Svc.MdbService.GetDataService<TStudio>())
                {
                    studioID = db.ExecuteQuery<int?>(
                        $"select {Svc.MdbService.GetStudioModelIdentity(studioModelName)} from {studioModelName} where sosync_fso_id = @sosync_fso_id",
                        new { sosync_fso_id = onlineID })
                        .SingleOrDefault();
                }
            }

            return studioID;
        }

        /// <summary>
        /// Checks the current run_count value of the job against the
        /// specified maximum value. Throws an <see cref="RunCountException"/>
        /// if the maximum value is reached or exceeded.
        /// </summary>
        /// <param name="maxRuns">The number of runs upon an exception is raised.</param>
        protected void CheckRunCount(int maxRuns)
        {
            Stopwatch s = new Stopwatch();
            s.Start();
            try
            {
                if (Job.Job_Run_Count >= maxRuns)
                    throw new RunCountException(Job.Job_Run_Count, maxRuns);
            }
            catch (Exception ex)
            {
                UpdateJobError(SosyncError.RunCounter, $"1) Run counter:\n{ex.ToString()}");
                throw;
            }
            s.Stop();
            LogMs(0, "RunCount", Job.ID, s.ElapsedMilliseconds);
        }

        /// <summary>
        /// Checks if the specified write date is still the same in the source system.
        /// </summary>
        /// <param name="job">The job to be checked.</param>
        /// <param name="sosyncWriteDate">The write date of the job since the last read.</param>
        /// <returns></returns>
        protected bool IsConsistent(SyncJob job, DateTime? sosyncWriteDate, Stopwatch consistencyWatch)
        {
            if (sosyncWriteDate == null)
            {
                Svc.Log.LogInformation($"Job ({job.ID}) Skipping consistency check, no {nameof(sosyncWriteDate)} present.");
                return true;
            }

            var maxMS = 250;

            if (consistencyWatch.ElapsedMilliseconds < maxMS)
            {
                Svc.Log.LogInformation($"Job ({job.ID}) Skipping consistency check, last check under {maxMS}ms.");
                return true;
            }

            Svc.Log.LogInformation($"Job ({job.ID}) Checking model consistency");

            ModelInfo currentInfo = null;

            if (job.Sync_Source_System == SosyncSystem.FSOnline.Value)
                currentInfo = GetOnlineInfo(job.Sync_Source_Record_ID.Value);
            else
                currentInfo = GetStudioInfo(job.Sync_Source_Record_ID.Value);

            consistencyWatch.Stop();
            consistencyWatch.Reset();
            consistencyWatch.Start();

            // Do not use any tolerance here. It's a check to see if the provided write date
            // is different from what is currently in the model
            if (sosyncWriteDate.HasValue && sosyncWriteDate.Value == (currentInfo.SosyncWriteDate ?? currentInfo.WriteDate).Value)
                return true;

            return false;
        }

        protected void UpdateSyncTargetDataBeforeUpdate(string data)
        {
            Svc.Log.LogInformation($"Updating job {Job.ID}: Sync_Target_Data_Before_Update");

            using (var db = GetDb())
            {
                Job.Sync_Target_Data_Before = data;
                Job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateSyncTargetDataBeforeUpdate), db, Job);
            }
        }

        protected void UpdateSyncSourceData(string data)
        {
            Svc.Log.LogInformation($"Updating job {Job.ID}: Sync_Source_Data");

            using (var db = GetDb())
            {
                Job.Sync_Source_Data = data;
                Job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateSyncSourceData), db, Job);
            }
        }

        protected void UpdateSyncTargetRequest(string requestData)
        {
            Svc.Log.LogInformation($"Updating job {Job.ID}: Sync_Target_Request");

            using (var db = GetDb())
            {
                Job.Sync_Target_Request = requestData;
                Job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateSyncTargetRequest), db, Job);
            }
        }

        protected void UpdateSyncTargetAnswer(string answerData, int? createdID)
        {
            Svc.Log.LogInformation($"Updating job {Job.ID}: Sync_Target_Answer");

            using (var db = GetDb())
            {
                if (createdID.HasValue)
                    Job.Sync_Target_Record_ID = createdID.Value;

                Job.Sync_Target_Answer = answerData;
                Job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateSyncTargetAnswer), db, Job);
            }
        }

        /// <summary>
        /// Updates the sync source data and log field.
        /// </summary>
        /// <param name="job">The job to be updated.</param>
        /// <param name="srcSystem">The sync source system.</param>
        /// <param name="srcModel">The sync source model.</param>
        /// <param name="srcId">The sync source ID.</param>
        /// <param name="log">Information to be logged.</param>
        protected void UpdateJobSourceAndTarget(SyncJob job, SosyncSystem srcSystem, string srcModel, int? srcId, SosyncSystem targetSystem, string targetModel, int? targetId, string log)
        {
            Svc.Log.LogInformation($"Updating job {job.ID}: check source");

            using (var db = GetDb())
            {
                job.Sync_Source_System = srcSystem?.Value;
                job.Sync_Source_Model = srcModel;
                job.Sync_Source_Record_ID = srcId;

                job.Sync_Target_System = targetSystem?.Value;
                job.Sync_Target_Model = targetModel;
                job.Sync_Target_Record_ID = targetId;

                job.Job_Log = (job.Job_Log ?? "") + (log ?? "");
                job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobSourceAndTarget), db, job);
            }
        }

        /// <summary>
        /// Updates the job, indicating that child processing started.
        /// </summary>
        /// <param name="job">The job to be updated.</param>
        private void UpdateJobChildStart(SyncJob job)
        {
            Svc.Log.LogInformation($"Updating job { job.ID}: child start");

            using (var db = GetDb())
            {
                job.Child_Job_Start = DateTime.UtcNow;
                job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobChildStart), db, job);
            }
        }

        protected void UpdateJob(SyncJob job, string description)
        {
            Svc.Log.LogInformation($"Updating job { Job.ID}: {description}");

            using (var db = GetDb())
            {
                Job.Write_Date = DateTime.UtcNow;

                UpdateJob(description, db, job);
            }
        }

        /// <summary>
        /// Updates the job, indicating that child processing finished.
        /// </summary>
        private void UpdateJobChildEnd()
        {
            Svc.Log.LogInformation($"Updating job {Job.ID}: child end");

            using (var db = GetDb())
            {
                Job.Child_Job_End = DateTime.UtcNow;
                Job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobChildEnd), db, Job);
            }
        }

        /// <summary>
        /// Updates the job, indicating that the tansformation started.
        /// </summary>
        private void UpdateJobSyncStart()
        {
            Svc.Log.LogInformation($"Updating job {Job.ID}: transformation/sync start");

            using (var db = GetDb())
            {
                Job.Sync_Start = DateTime.UtcNow;
                Job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobSyncStart), db, Job);
            }
        }

        /// <summary>
        /// Updates the job, indicating that the transformation finished.
        /// </summary>
        private void UpdateJobSyncEnd()
        {
            Svc.Log.LogInformation($"Updating job { Job.ID}: transformation/sync end");

            using (var db = GetDb())
            {
                Job.Sync_End = DateTime.UtcNow;
                Job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobSyncEnd), db, Job);
            }
        }

        /// <summary>
        /// Updates the job, indicating processing started.
        /// </summary>
        private void UpdateJobStart(SyncJob job, DateTime loadTimeUTC)
        {
            Svc.Log.LogInformation($"Updating job {job.ID}: job start");

            using (var db = GetDb())
            {
                job.Job_Run_Count += 1;
                job.Job_State = SosyncState.InProgress.Value;
                job.Job_Start = loadTimeUTC;
                job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobStart), db, job);
            }
        }

        private void UpdateJobInconsistentBlock(SyncJob job)
        {
            Svc.Log.LogInformation($"Updating job {job.ID}: job_log");

            using (var db = GetDb())
            {
                job.Job_Log += $"AddresseBlock check failed, exiting job, leaving it in progress.\n";
                job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobInconsistent), db, job);
            }
        }

        private void UpdateJobInconsistent(SyncJob job, int nr)
        {
            Svc.Log.LogInformation($"Updating job {job.ID}: job_log");

            using (var db = GetDb())
            {
                job.Job_Log += $"Consistency check {nr} failed, exiting job, leaving it in progress.\n";
                job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobInconsistent), db, job);
            }
        }

        /// <summary>
        /// Updates the job, indicating the processing was
        /// done successfully.
        /// </summary>
        /// <param name="wasInSync">Set to true, if the job was
        /// finished because it was already in sync.</param>
        protected void UpdateJobSuccess(bool wasInSync)
        {
            Svc.Log.LogInformation($"Updating job {Job.ID}: job done {(wasInSync ? " (model already in sync)" : "")}");

            using (var db = GetDb())
            {
                Job.Job_State = SosyncState.Done.Value;
                Job.Job_End = DateTime.UtcNow;
                Job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobSuccess), db, Job);
            }
        }

        protected void UpdateJobSkipped()
        {
            Svc.Log.LogInformation($"Updating job {Job.ID}: job skipped");

            using (var db = GetDb())
            {
                Job.Job_State = SosyncState.Skipped.Value;
                Job.Job_End = DateTime.UtcNow;
                Job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobSkipped), db, Job);
            }
        }


        /// <summary>
        /// Updates the job, indicating the processing was
        /// done successfully in another thread.
        /// </summary>
        /// <param name="otherJobID">ID of the job that led to completion of this job.</param>
        protected void UpdateJobSuccessOtherThread(int otherJobID)
        {
            var msg = $"Updating job {Job.ID}: Another thread completed same job (ID {otherJobID})";
            Svc.Log.LogInformation(msg);

            using (var db = GetDb())
            {
                Job.Job_State = SosyncState.Done.Value;
                Job.Job_Log += "\n" + msg;
                Job.Job_End = DateTime.UtcNow;
                Job.Write_Date = DateTime.UtcNow;

                UpdateJob(nameof(UpdateJobSuccess), db, Job);
            }
        }


        /// <summary>
        /// Updates the job, indicating an an error occured while processing.
        /// </summary>
        /// <param name="error">Use one of the values from <see cref="SosyncError"/>.</param>
        /// <param name="errorText">The custom error text.</param>
        protected void UpdateJobError(SosyncError error, string errorText, bool useErrorRetry = false)
        {
            Svc.Log.LogInformation($"Updating job {Job.ID}: job error");

            using (var db = GetDb())
            {
                JobHelper.SetJobError(Job, error, errorText);
                UpdateJob(nameof(UpdateJobError), db, Job);
            }
        }

        private void UpdateJob(string method, DataService db, SyncJob job)
        {
            Stopwatch s = new Stopwatch();
            s.Start();

            db.UpdateJob(job);

            s.Stop();
            LogMs(1, $"Local DB: {method}", job.ID, s.ElapsedMilliseconds);
        }

        protected void LogMs(int lvl, string name, int? jobId, long ms)
        {
            Svc.Log.LogInformation($"Job {jobId ?? Job.ID}: {name} elapsed: {ms} ms");
            LogMilliseconds(name, ms);
        }

        public void Dispose()
        {

        }
        #endregion
    }
}
