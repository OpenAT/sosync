using Npgsql;
using DaDi.Odoo;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Dapper;
using MassDataCorrection.Models;
using MassDataCorrection.Properties;
using DaDi.Odoo.Models;
using WebSosync.Data.Models;
using WebSosync.Data;
using dadi_data.Models;
using dadi_data;

namespace MassDataCorrection
{
    class Program
    {
        static void Main(string[] args)
        {
            //var repoBasePath = @"C:\WorkingFolder\saltstack";

            //Console.WriteLine("Make sure the saltstack repository path is correct:");
            //Console.WriteLine(repoBasePath);

            //Console.Write("\nMake sure the repository is up to date.\nPress [Y] to continue: ");
            //var key = Console.ReadKey();
            //Console.WriteLine();

            //if (key.KeyChar == 'y' || key.KeyChar == 'Y')
            //{
            //    var processor = new PillarProcessor(Path.Combine(repoBasePath, "pillar", "instances"));

            //    //processor.Process(InitialSyncPayments, new[] { "demo" });
            //    processor.Process(CheckBranch, null);

            //    Console.WriteLine("\nDone. Press any key to quit.");
            //}
            //else
            //{
            //    Console.WriteLine("\nCancelled. Press any key to quit.");
            //}

            TestAddress();

            Console.ReadKey();
        }

        private static void TestAddress()
        {
            using (var db = new DataService<dboPersonAdresse>("Data Source=mssql1; Initial Catalog=mdb_dev1; Integrated Security=true;"))
            {
                var model = db.Read(new { PersonAdresseID = 2485600 })
                    .SingleOrDefault();

                model.Postfach = "TEST-MKA";
                db.Update(model);
            }
        }

        private static void PrintDictionary<T>(Dictionary<string, T> dic)
        {
            foreach (var item in dic)
            {
                Console.WriteLine($"{item.Key}: {item.Value}");
            }
        }

        #region Older checks

        private static Dictionary<string, int> _tel = new Dictionary<string, int>();
        private static void CheckFaultyPhoneNumbers(InstanceInfo info, Action<float> reportProgress)
        {
            try
            {
                using (var con = info.CreateOpenMssqlConnection())
                {
                    var cmd = new SqlCommand(@"
	                    SELECT
		                    COUNT(*) Anzahl
	                    FROM
		                    PersonTelefon t
	                    WHERE
		                    (ISNULL(t.Landkennzeichen, '') = '' OR ISNULL(t.Vorwahl, '') = '')
		                    AND ISNULL(t.Rufnummer, '') <> ''
                        ", con);

                    var count = Convert.ToInt32(cmd.ExecuteScalar());

                    if (count > 0)
                        _tel[info.Instance] = count;

                    Console.WriteLine($"Anzahl Tel: {count}");
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Could not connect to mssql.");
            }
        }

        private static void CorrectFaultyPhoneNumbers(InstanceInfo info, Action<float> reportProgress)
        {
            try
            {
                using (var con = info.CreateOpenMssqlConnection())
                {
                    var cmd = new SqlCommand(@"
                        SELECT COUNT(*) FROM dbo.PersonTelefon t
	                    WHERE
		                    (ISNULL(t.Landkennzeichen, '') = '' OR ISNULL(t.Vorwahl, '') = '')
		                    AND ISNULL(t.Rufnummer, '') <> '';

                        SELECT
		                    t.PersonTelefonID
                            ,t.PersonID
                            ,Rufnummer
	                    FROM
		                    PersonTelefon t
	                    WHERE
		                    (ISNULL(t.Landkennzeichen, '') = '' OR ISNULL(t.Vorwahl, '') = '')
		                    AND ISNULL(t.Rufnummer, '') <> '';
                        ", con);

                    var rdr = cmd.ExecuteReader();
                    var count = 0;

                    if (rdr.Read())
                        count = Convert.ToInt32(rdr[0]);

                    while (rdr.Read())
                    {

                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Could not connect to mssql.");
            }
        }

        private static void CheckOpenSyncJobs(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.host_sosync != "sosync2")
            {
                Console.WriteLine($"Skipping {info.Instance}, host_sosync is not sosync2.");
                return;
            }

            using (var con = info.CreateOpenSyncerNpgsqlConnection())
            {
                var cmd = new NpgsqlCommand("select job_source_type, job_source_model, count(*) cnt from sosync_job where job_state = 'new' group by job_source_type, job_source_model;", con);

                var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    var t = (string)(rdr["job_source_type"] == DBNull.Value ? "<null>" : rdr["job_source_type"]);

                    Console.WriteLine($" - [{t}] {rdr["job_source_model"]}: {rdr["cnt"]}");
                }
            }
        }

        private static void FillNewDonationReportFields(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.host_sosync != "sosync2")
            {
                Console.WriteLine($"Skipping {info.Instance}, host_sosync is not sosync2.");
                return;
            }

            SqlMapper.AddTypeMap(typeof(DateTime), System.Data.DbType.DateTime2);
            SqlMapper.AddTypeMap(typeof(DateTime?), System.Data.DbType.DateTime2);

            using (var pgCon = info.CreateOpenNpgsqlConnection())
            using (var msCon = info.CreateOpenMssqlConnection())
            {
                var pgDonationReports = pgCon
                    .Query<PgDonationReportCorrection>("select id, sosync_fs_id, submission_id_datetime, response_error_orig_refnr from res_partner_donation_report where submission_env = 'P';")
                    .ToList();

                if (info.Instance == "bsvw")
                {
                    pgDonationReports.Remove(pgDonationReports.Where(x => x.id == 22992).SingleOrDefault());
                    pgDonationReports.Remove(pgDonationReports.Where(x => x.id == 42484).SingleOrDefault());
                }

                var msDonationReports = msCon
                    .Query<MsDonationReportCorrection>("select AktionsID, sosync_fso_id, SubmissionIdDate, ResponseErrorOrigRefNr from AktionSpendenmeldungBPK")
                    .ToDictionary(x => x.AktionsID);

                var updated = 0;
                for (int i = 0; i < pgDonationReports.Count; i++)
                {
                    var msDonationReport = msDonationReports.ContainsKey(pgDonationReports[i].sosync_fs_id)
                        ? msDonationReports[pgDonationReports[i].sosync_fs_id]
                        : null;

                    if (msDonationReport != null)
                    {
                        var update = false;
                        if (!IsEqualOdooMssqlDate(msDonationReport.SubmissionIdDate, pgDonationReports[i].submission_id_datetime))
                        {
                            //Console.WriteLine($"Date different ({pgDonationReports[i].sosync_fs_id}): [MSSQL] {GetDate(msDonationReport.SubmissionIdDate)} and [PGSQL] {GetDate(pgDonationReports[i].submission_id_datetime)}.");
                            update = true;
                            if (pgDonationReports[i].submission_id_datetime.HasValue)
                                msDonationReport.SubmissionIdDate = pgDonationReports[i].submission_id_datetime.Value.ToLocalTime();
                            else
                                msDonationReport.SubmissionIdDate = null;
                        }

                        if ((msDonationReport.ResponseErrorOrigRefNr ?? "") != (pgDonationReports[i].response_error_orig_refnr ?? ""))
                        {
                            //Console.WriteLine($"RefNR different");
                            update = true;
                            msDonationReport.ResponseErrorOrigRefNr = pgDonationReports[i].response_error_orig_refnr;
                        }

                        if (update)
                        {
                            msCon.Execute("UPDATE dbo.AktionSpendenmeldungBPK SET SubmissionIdDate = @SubmissionIdDate, ResponseErrorOrigRefnr = @ResponseErrorOrigRefnr, noSyncJobSwitch = 1 WHERE AktionsID = @AktionsID",
                                new { AktionsID = msDonationReport.AktionsID, SubmissionIdDate = msDonationReport.SubmissionIdDate, ResponseErrorOrigRefnr = msDonationReport.ResponseErrorOrigRefNr });

                            updated++;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Not synced: res_partner_donation_report.id = {pgDonationReports[i].id}");
                    }

                    reportProgress((float)(i + 1) / (float)pgDonationReports.Count);
                }

                Console.WriteLine($"\nUpdated {updated} of {pgDonationReports.Count} donation reports.");
            }
        }

        private static void FillMissingPartnerBPKFields(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.host_sosync != "sosync2")
            {
                Console.WriteLine($"Skipping {info.Instance}, host_sosync is not sosync2.");
                return;
            }

            SqlMapper.AddTypeMap(typeof(DateTime), System.Data.DbType.DateTime2);
            SqlMapper.AddTypeMap(typeof(DateTime?), System.Data.DbType.DateTime2);

            using (var pgCon = info.CreateOpenNpgsqlConnection())
            using (var msCon = info.CreateOpenMssqlConnection())
            {
                var pgPartnerBPKs = pgCon
                    .Query<PgPartnerBpkCorrection>("select id, sosync_fs_id, bpk_request_zip, bpk_error_request_zip, bpk_request_log, last_bpk_request, bpk_request_url, bpk_error_request_url, state from res_partner_bpk;")
                    .ToList();

                var msPersonBPKs = msCon
                    .Query<MsPartnerBpkCorrection>("select PersonBPKID, sosync_fso_id, PLZ, FehlerPLZ, RequestLog, LastRequest, RequestUrl, ErrorRequestUrl, fso_state from PersonBPK;")
                    .ToDictionary(x => x.PersonBPKID);

                var updated = 0;
                for (int i = 0; i < pgPartnerBPKs.Count; i++)
                {
                    var msPersonBPK = msPersonBPKs.ContainsKey(pgPartnerBPKs[i].sosync_fs_id)
                        ? msPersonBPKs[pgPartnerBPKs[i].sosync_fs_id]
                        : null;

                    if (msPersonBPK != null)
                    {
                        var update = false;

                        if (HasBPKChanges(msPersonBPK, pgPartnerBPKs[i]))
                        {
                            //Console.WriteLine($"RefNR different");
                            update = true;
                            CopyBPK(pgPartnerBPKs[i], msPersonBPK);
                        }

                        if (update)
                        {
                            msCon.Execute("UPDATE dbo.PersonBPK SET PLZ = @PLZ, FehlerPLZ = @FehlerPLZ, RequestLog = @RequestLog, LastRequest = @LastRequest, RequestUrl = @RequestUrl, ErrorRequestUrl = @ErrorRequestUrl, fso_state = @fso_state, noSyncJobSwitch = 1 WHERE PersonBPKID = @PersonBPKID;",
                                new {
                                    PersonBPKID = msPersonBPK.PersonBPKID,
                                    PLZ = msPersonBPK.PLZ,
                                    FehlerPLZ = msPersonBPK.FehlerPLZ,
                                    RequestLog = msPersonBPK.RequestLog,
                                    LastRequest = msPersonBPK.LastRequest,
                                    RequestUrl = msPersonBPK.RequestUrl,
                                    ErrorRequestUrl = msPersonBPK.ErrorRequestUrl,
                                    fso_state = msPersonBPK.fso_state
                                });

                            updated++;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Not synced: res_partner_bpk.id = {pgPartnerBPKs[i].id}");
                    }

                    reportProgress((float)(i + 1) / (float)pgPartnerBPKs.Count);
                }

                Console.WriteLine($"\nUpdated {updated} of {pgPartnerBPKs.Count} PersonBPKs.");
            }
        }

        private static bool HasBPKChanges(MsPartnerBpkCorrection msBPK, PgPartnerBpkCorrection pgBPK)
        {
            var result =
                (msBPK.PLZ ?? "") != (pgBPK.bpk_request_zip ?? "")
                || (msBPK.FehlerPLZ ?? "") != (pgBPK.bpk_error_request_zip ?? "")
                || (msBPK.RequestLog ?? "") != (pgBPK.bpk_request_log ?? "")
                || msBPK.LastRequest != pgBPK.last_bpk_request
                || (msBPK.RequestUrl ?? "") != (pgBPK.bpk_request_url ?? "")
                || (msBPK.ErrorRequestUrl ?? "") != (pgBPK.bpk_error_request_url ?? "")
                || (msBPK.fso_state ?? "") != (pgBPK.state ?? "");

            return result;
        }

        private static void CopyBPK(PgPartnerBpkCorrection source, MsPartnerBpkCorrection dest)
        {
            dest.PLZ = source.bpk_request_zip;
            dest.FehlerPLZ = source.bpk_error_request_zip;
            dest.RequestLog = source.bpk_request_log;
            dest.LastRequest = source.last_bpk_request;
            dest.RequestUrl = source.bpk_request_url;
            dest.ErrorRequestUrl = source.bpk_error_request_url;
            dest.fso_state = source.state;
        }

        private static string GetDate(DateTime? d)
        {
            if (d.HasValue)
                return d.Value.ToString("yyyy-MM-dd HH:mm:ss.ffffff");

            return "<NULL>";
        }

        private static bool IsEqualOdooMssqlDate(DateTime? mssqlNormalDate, DateTime? odooUtcDate)
        {
            if (!mssqlNormalDate.HasValue && !odooUtcDate.HasValue)
                return true;

            if (mssqlNormalDate.HasValue && odooUtcDate.HasValue)
                return mssqlNormalDate.Value.ToUniversalTime() == odooUtcDate.Value;

            return false;
        }

        private static void DeleteAndResyncFiscalYears(InstanceInfo info, Action<float> reportProgress)
        {
            var enabled = false;

            if (info.host_sosync != "sosync2")
            {
                Console.WriteLine($"Skipping {info.Instance}, host_sosync is not sosync2.");
                return;
            }

            // 1) Delete all xBPKMeldespanne
            using (var con = info.CreateOpenMssqlConnection())
            {
                var years = con.ExecuteScalar<int>("select count(*) from dbo.xBPKMeldespanne");
                Console.WriteLine($"xBPKMeldespannen to delete: {years}");

                if (enabled)
                    con.Execute("delete from dbo.xBPKMeldespanne");
            }

            // 2) Set all sosync_fs_id in pgSQL to null
            List<int> yearIDs;
            using (var con = info.CreateOpenNpgsqlConnection())
            {
                yearIDs = con.Query<int>("select id from account_fiscalyear")
                    .ToList();

                Console.WriteLine($"FiscalYears to reset for sync: {yearIDs.Count}");

                if (enabled)
                    con.Execute("update account_fiscalyear set sosync_fs_id = null");
            }

            // 3) Use XML-RPC to invoke new sync jobs for fiscal years
            if (enabled)
            {
                Console.WriteLine($"Creating {yearIDs.Count} new sync jobs.");
                var client = info.CreateAuthenticatedOdooClient();
                client.CreateSyncJob("account.fiscalyear", yearIDs.ToArray());

                // yearIDs.ForEach(x => client.CreateSyncJob("account.fiscalyear", x));
            }
        }

        private static Dictionary<string, int> _unsyncedPartners = new Dictionary<string, int>();
        private static void CheckUnsyncedPartners(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.host_sosync != "sosync2")
            {
                Console.WriteLine($"Skipping {info.Instance}, host_sosync is not sosync2.");
                return;
            }

            using (var db = info.CreateOpenNpgsqlConnection())
            {
                var query = "select count(*) from res_partner where active = true and coalesce(sosync_fs_id, 0) = 0;";
                var count = db.Query<int>(query).SingleOrDefault();

                _unsyncedPartners.Add(info.Instance, count);

                Console.WriteLine($"Unsynchronized Partners: {count}");
            }
        }

        private static void ShowUnsynchedPartners()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Summary:");
            Console.ForegroundColor = ConsoleColor.Gray;

            foreach (var item in _unsyncedPartners)
            {
                Console.WriteLine($"{item.Key}: {item.Value}");
            }
        }

        private static Dictionary<string, int> groupsStats = new Dictionary<string, int>();
        private static void CheckGroups(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.host_sosync != "sosync2")
            {
                Console.WriteLine($"Skipping {info.Instance}, host_sosync is not sosync2.");
                return;
            }

            var rpc = info.CreateAuthenticatedOdooClient();

            using (var msCon = info.CreateOpenMssqlConnection())
            {
                // msCon.Query(Resources.UpdateSosyncWriteDate);

                var data = msCon.Query<NlRow>(Resources.GruppenQuery)
                    .ToList();

                var anzahl = 0;
                foreach (var row in data)
                {
                    // Console.WriteLine($"Creating SyncJob for \"res.partner\" {row.sosync_fso_id}");
                    Console.WriteLine($"PersonGruppeID {row.Tabellenidentity} GültigBis auf 31.12.2099 gesetzt");
                    rpc.CreateSyncJob("res.partner", row.sosync_fso_id);
                    anzahl++;
                }

                if (!groupsStats.ContainsKey(info.Instance))
                    groupsStats.Add(info.Instance, anzahl);

                Console.WriteLine($"Anzahl: {anzahl}");
            }
        }

        private static Dictionary<string, int> _restartBpkCounts = new Dictionary<string, int>();
        private static void RestartBpkJobs(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.host_sosync != "sosync2")
            {
                Console.WriteLine($"Skipping {info.Instance}, host_sosync is not sosync2.");
                return;
            }

            var rpc = info.CreateAuthenticatedOdooClient();

            var searchArgs = new List<OdooSearchArgument>();
            searchArgs.Add(new OdooSearchArgument("job_source_model", "=", "res.partner.bpk"));
            searchArgs.Add(new OdooSearchArgument("job_run_count", ">=", "5"));
            searchArgs.Add(new OdooSearchArgument("job_state", "=", "error"));
            searchArgs.Add(new OdooSearchArgument("job_error_text", "like", "aktuelle Transaktion kann kein Commit"));

            var data = rpc.SearchBy("sosync.job", searchArgs);

            _restartBpkCounts.Add(info.Instance, data.Length);

            var ids = new List<int>();
            var current = 0;
            foreach (var jobID in data)
            {
                current++;
                Console.CursorLeft = 0;
                var percent = (int)((float)current / (float)data.Length * 100f);
                Console.Write($"{percent}%");

                try
                {
                    var job = rpc.GetDictionary("sosync.job", jobID, new string[] { "job_source_record_id" });
                    var id = Convert.ToInt32(job["job_source_record_id"]);

                    if (!ids.Contains(id))
                    {
                        ids.Add(id);
                        rpc.RunMethod("res.partner.bpk", "create_sync_job", id);
                    }
                }
                catch (Exception)
                {
                }
            }
            Console.WriteLine();
        }

        private static Dictionary<string, int> _restartDonationReportsCounts = new Dictionary<string, int>();
        private static void RestartDonationReportJobs(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.host_sosync != "sosync2")
            {
                Console.WriteLine($"Skipping {info.Instance}, host_sosync is not sosync2.");
                return;
            }

            var rpc = info.CreateAuthenticatedOdooClient();

            var searchArgs = new List<OdooSearchArgument>();
            searchArgs.Add(new OdooSearchArgument("job_source_model", "=", "dbo.AktionSpendenmeldungBPK"));
            searchArgs.Add(new OdooSearchArgument("job_run_count", ">=", "5"));
            searchArgs.Add(new OdooSearchArgument("job_state", "=", "error"));
            //searchArgs.Add(new OdooSearchArgument("job_error_text", "like", "aktuelle Transaktion kann kein Commit"));

            var data = rpc.SearchBy("sosync.job", searchArgs);

            _restartDonationReportsCounts.Add(info.Instance, data.Length);

            var ids = new List<int>();
            var current = 0;
            foreach (var jobID in data)
            {
                current++;
                Console.CursorLeft = 0;
                var percent = (int)((float)current / (float)data.Length * 100f);
                Console.Write($"{percent}%");

                try
                {
                    var job = rpc.GetDictionary("sosync.job", jobID, new string[] { "job_source_record_id" });
                    var id = Convert.ToInt32(job["job_source_record_id"]);

                    using (var mdb = info.CreateOpenMssqlConnection())
                    {
                        mdb.Execute(Properties.Resources.SubmitSpendenmeldungSyncJob, new { ID = id });
                    }

                    //if (!ids.Contains(id))
                    //{
                    //    ids.Add(id);
                    //    rpc.RunMethod("res.partner.donation_report", "create_sync_job", id);
                    //}
                }
                catch (Exception)
                {
                }
            }
            Console.WriteLine();
        }

        private static Dictionary<string, string> _checkDonationReport = new Dictionary<string, string>();
        private static void CheckDonationReports(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.host_sosync != "sosync2")
                return;

            var skip = new[] {
                "dadi"
                ,"aahs"
                ,"apfo"
                ,"avnc"
                ,"deve"
                ,"rnga"
                ,"tiqu"
                ,"diak"
                ,"jeki"
                ,"kich"
                ,"myeu"
                ,"nphc"
                ,"otiv"
                ,"rnde"
                ,"wdcs"
                ,"rona"
            };

            if (skip.Contains(info.Instance))
            {
                Console.WriteLine($"Skipping {info.Instance}");
                return;
            }

            Dictionary<int, DonationReport> fsoDonations = null;
            Dictionary<int, DonationReport> fsDonations = null;

            if (info.host_sosync == "sosync2")
                CheckSosync2Donations(info, reportProgress, out fsoDonations, out fsDonations);
            else if (info.host_sosync == "sosync1")
                CheckSosync1Donations(info, reportProgress, out fsoDonations, out fsDonations);

            var rpc = info.CreateAuthenticatedOdooClient();

            // Compare results

            using (var db = info.CreateOpenMssqlIntegratedConnection())
            {
                var diffCount = 0;
                var missingCount = 0;
                foreach (var item in fsDonations)
                {
                    var fsItem = item.Value;
                    DonationReport fsoItem = null;

                    if (fsoDonations.ContainsKey(item.Value.ForeignID))
                        fsoItem = fsoDonations[item.Value.ForeignID];

                    if (fsoItem != null)
                    {
                        if (fsItem.State != fsoItem.State)
                        {
                            try
                            {
                                diffCount++;
                                Console.WriteLine($"Different state for AktionsID {item.Key} / res.partner.donation_report.id {item.Value.ForeignID}");

                                //db.Execute($@"
                                //UPDATE dbo.AktionSpendenmeldungBPK
                                //SET sosync_write_date = DATEADD(HOUR, -3, sosync_write_date)
                                //WHERE AktionsID = @AktionsID;", new { AktionsID = item.Key });

                                //var i = db.Execute(@"
                                //insert into sosync.JobQueue
                                //(
                                //    JobDate
                                //    ,JobSourceSystem
                                //    ,JobSourceModel
                                //    ,JobSourceRecordID
                                //    ,JobState
                                //    ,JobSourceSosyncWriteDate
                                //    ,JobSourceFields
                                //)
                                //values (
                                //    @JobDate
                                //    ,@JobSourceSystem
                                //    ,@JobSourceModel
                                //    ,@JobSourceRecordID
                                //    ,@JobState
                                //    ,@JobSourceSosyncWriteDate
                                //    ,@JobSourceFields
                                //)
                                //",
                                //    new
                                //    {
                                //        JobDate = fsItem.SosyncWriteDate.Value,
                                //        JobSourceSystem = SosyncSystem.FundraisingStudio,
                                //        JobSourceModel = "dbo.AktionSpendenmeldungBPK",
                                //        JobSourceRecordID = fsItem.ID,
                                //        JobState = "new",
                                //        JobSourceSosyncWriteDate = fsItem.SosyncWriteDate.Value,
                                //        JobSourceFields = $"{{ \"sosync_write_date\": \"{fsItem.SosyncWriteDate.Value.ToString("yyyy-MM-dd HH:mm:ss.fffffff")}\" }}"
                                //    });

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                        }
                    }
                    else
                    {
                        missingCount++;
                    }
                }

                Console.WriteLine($"Differences: {diffCount} {(missingCount > 0 ? $"Missing: {missingCount}" : "")} ({info.host_sosync})");

                if (diffCount > 0 || missingCount > 0)
                    _checkDonationReport.Add(info.Instance, $"{diffCount} {(missingCount > 0 ? $"Missing: {missingCount}" : "")} ({info.host_sosync})");
            }
        }

        private static void CheckSosync2Donations(
            InstanceInfo info, 
            Action<float> reportProgress, 
            out Dictionary<int, DonationReport> fsoDonations, 
            out Dictionary<int, DonationReport>  fsDonations)
        {
            using (var onlineCon = info.CreateOpenNpgsqlConnection())
            {
                fsoDonations = onlineCon
                    .Query<DonationReport>("select id ID, sosync_fs_id ForeignID, state State, sosync_write_date SosyncWriteDate from res_partner_donation_report")
                    .ToDictionary(x => x.ID);
            }

            using (var fsCon = info.CreateOpenMssqlConnection())
            {
                fsDonations = fsCon
                    .Query<DonationReport>("select AktionsID ID, sosync_fso_id ForeignID, Status State, sosync_write_date SosyncWriteDate from dbo.AktionSpendenmeldungBPK")
                    .ToDictionary(x => x.ID);
            }
        }

        private static void CheckSosync1Donations(
            InstanceInfo info,
            Action<float> reportProgress,
            out Dictionary<int, DonationReport> fsoDonations,
            out Dictionary<int, DonationReport> fsDonations)
        {
            using (var onlineCon = info.CreateOpenNpgsqlConnection())
            {
                fsoDonations = onlineCon
                    .Query<DonationReport>("select id ID, sosync_fs_id ForeignID, state State, null SosyncWriteDate from res_partner_donation_report")
                    .ToDictionary(x => x.ID);
            }

            using (var fsCon = info.CreateOpenMssqlIntegratedConnection())
            {
                fsDonations = fsCon
                    .Query<DonationReport>(@"SELECT
	                                            mssqlBPK.AktionsID ID
	                                            ,odooBPK.id ForeignID
	                                            ,odooBPK.state
	                                            ,mssqlBPK.sosync_write_date SosyncWriteDate 
                                            FROM
	                                            dbo.xAktionSpendenmeldungBPK mssqlBPK
	                                            INNER JOIN odoo.res_partner_donation_report odooBPK
		                                            ON mssqlBPK.AktionsID = odooBPK.MDBID
                                            ")
                    .ToDictionary(x => x.ID);
            }
        }

        #endregion

        #region Current checks

        private static void InitialSync(string modelName, OdooClient rpc, NpgsqlConnection onlineCon, bool unsyncedOnly)
        {
            var query = $@"
                select id, sosync_fs_id, sosync_write_date from {modelName.Replace(".", "_")}
                {(unsyncedOnly ? " where coalesce(sosync_fs_id, 0) = 0" : "")}
                ";

            var models = onlineCon.Query(query);

            foreach (var model in models)
            {
                string dummy = $"ID {model.id}";
                var id = rpc.CreateModel("sosync.job.queue", new
                {
                    job_date = model.sosync_write_date ?? DateTime.UtcNow,
                    job_source_system = "fso",
                    job_source_model = modelName,
                    job_source_record_id = (int)model.id,
                    job_source_target_record_id = (int?)model.sosync_fs_id,
                    job_source_sosync_write_date = model.sosync_write_date,
                    //submission_url = submissionUrl
                });
            }
        }

        private static void CheckBranch(InstanceInfo info, Action<float> reportProgress)
        {
            var desiredBranch = "v2";

            if (info.sosync_branch != desiredBranch)
                throw new Exception($"{info.Instance} is not on branch {desiredBranch}");
        }


        private static void InitialSyncPayments(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.host_sosync != "sosync2")
                return;

            var skip = new string[] {
                //"dadi"
                //,"aahs"
                //,"apfo"
                //,"avnc"
                //,"deve"
                //,"rnga"
                //,"tiqu"
                //,"diak"
                //,"jeki"
                //,"kich"
                //,"myeu"
                //,"nphc"
                //,"otiv"
                //,"rnde"
                //,"wdcs"
                //,"rona"
            };

            if (skip.Contains(info.Instance))
            {
                Console.WriteLine($"Skipping {info.Instance}");
                return;
            }

            var rpc = info.CreateAuthenticatedOdooClient();
            using (var onlineCon = info.CreateOpenNpgsqlConnection())
            {
                InitialSync("payment.acquirer", rpc, onlineCon, false);
                InitialSync("payment.transaction", rpc, onlineCon, false);
                InitialSync("product.product", rpc, onlineCon, false);
                InitialSync("sale.order.line", rpc, onlineCon, false);
            }
        }
        #endregion
    }
}
