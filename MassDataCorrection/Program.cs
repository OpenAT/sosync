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
            var repoBasePath = @"C:\WorkingFolder\saltstack";

            Console.WriteLine("Make sure the saltstack repository path is correct:");
            Console.WriteLine(repoBasePath);

            Console.Write("\nMake sure the repository is up to date.\nPress [Y] to continue: ");
            var key = Console.ReadKey();
            Console.WriteLine();

            if (key.KeyChar == 'y' || key.KeyChar == 'Y')
            {
                var processor = new PillarProcessor(Path.Combine(repoBasePath, "pillar", "instances"));

                //processor.Process(InitialSyncPayments, new[] { "demo" });
                //processor.Process(CheckBranch, null);
                //processor.Process(CheckDonationReports, new[] { "bsvw" });
                //processor.Process(CheckEmails, new[] { "dev1" });
                //processor.Process(CheckPersonBPKs, null);

                //processor.Process(FixLostSyncJobs, null);
                //processor.Process((inst, prog) => CheckModel(inst, prog, "dbo.AktionSpendenmeldungBPK", "res.partner.donation_report", "status", "state"), new[] { "gl2k" });
                //processor.Process((inst, prog) => CheckModel(inst, prog, "fson.payment_transaction", "payment.transaction", "state", "state"), new[] { "gl2k" });
                //processor.Process((inst, prog) => CheckModel(inst, prog, "dbo.AktionSpendenmeldungBPK", "res.partner.donation_report", "Status", "state"), new[] { "aiat" });

                processor.Process((inst, prog) => CheckModel(inst, prog, "dbo.AktionSpendenmeldungBPK", "res.partner.donation_report", "Status", "state"), new[] { "aiat" });
                //processor.Process((inst, prog) => CheckModel(inst, prog, "dbo.Person", "res.partner", "Name", "lastname"), new[] { "aiat" });

                //processor.Process((inst, report) => GetSyncJobCount(inst, report), new[] { "proj", "diak" });
                //PrintDictionary(_jobCounts);

                //SaveStat(_missingModels, "models_missing");
                //SaveStat(_modelNotSync, "models_mismatch");

                //processor.Process(CheckOpenSyncJobs, null);
                //processor.Process(UpdateErrorJobs, new[] { "demo" });
                //processor.Process(UpdateErrorJobs, null);
                //processor.Process(GetArchiveProgress, new[] { "demo" });
                //processor.Process(GetArchiveProgress, null);
                //PrintDictionary(_archiveProgress);

                //SaveStat(_missingEmails, "emails_missing");
                //SaveStat(_emailsNotSync, "emails_mismatch");

                //SaveStat(_missingPartnerIDs, "person_missing");
                //SaveStat(_PersonIDsNotUpToDate, "person_mismatch");

                Console.WriteLine("\nDone. Press any key to quit.");
            }
            else
            {
                Console.WriteLine("\nCancelled. Press any key to quit.");
            }

            Console.ReadKey();
        }

        private static void SaveStat(Dictionary<string, List<int>> dictionary, string fileSuffix)
        {
            foreach (var kvp in dictionary)
            {
                File.WriteAllText($"{kvp.Key}_{fileSuffix}.txt", string.Join(",\r\n", kvp.Value));
                var fi = new FileInfo($"{kvp.Key}_{fileSuffix}.txt");
                Console.WriteLine($"Data written to {fi.FullName}");
            }
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
            if (info.Pillar.HostSosync != "sosync2")
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
            if (info.Pillar.HostSosync != "sosync2")
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
            if (info.Pillar.HostSosync != "sosync2")
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
                                new
                                {
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

            if (info.Pillar.HostSosync != "sosync2")
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
            if (info.Pillar.HostSosync != "sosync2")
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
            if (info.Pillar.HostSosync != "sosync2")
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
            if (info.Pillar.HostSosync != "sosync2")
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
            if (info.Pillar.HostSosync != "sosync2")
            {
                Console.WriteLine($"Skipping {info.Instance}, host_sosync is not sosync2.");
                return;
            }

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
                        mdb.Execute(
                            Resources.InsertSyncJobMSSQL,
                            new
                            {
                                ID = id,
                                model = "dbo.AktionSpendenmeldungBPK",
                                sosyncWriteDate = DateTime.UtcNow,
                                system = "fs",
                                jobSourceFields = "{ \"info\": \"manual\" }"
                            });
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


        private static Dictionary<string, Tuple<int, int>> _checkBpk = new Dictionary<string, Tuple<int, int>>();
        private static void CheckPersonBPKs(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.Pillar.HostSosync != "sosync2")
                return;

            var skip = new[] {
                "rnga"
                ,"tiqu"
                ,"kich"
                ,"nphc"
                ,"otiv"
                ,"rnde"
                ,"rona"
                ,"diak"
                };

            if (skip.Contains(info.Instance))
                return;

            Dictionary<int, PersonBPK> fsoBpks = null;
            Dictionary<int, PersonBPK> fsBpks = null;

            using (var onlineCon = info.CreateOpenNpgsqlConnection())
            {
                fsoBpks = onlineCon
                    .Query<PersonBPK>("select id ID, sosync_fs_id ForeignID, bpk_request_firstname Vorname, sosync_write_date, bpk_private BpkPrivat from res_partner_bpk")
                    .ToDictionary(x => x.ID);
            }

            using (var fsCon = info.CreateOpenMssqlConnection())
            {
                fsBpks = fsCon
                    .Query<PersonBPK>("select PersonBPKID ID, sosync_fso_id ForeignID, Vorname, sosync_write_date, BpkPrivat from dbo.PersonBPK")
                    .ToDictionary(x => x.ID);
            }

            // Compare data

            var bpkErrCount = 0;
            var notFoundCount = 0;

            foreach (var fsoKvp in fsoBpks)
            {
                var fsoBPK = fsoKvp.Value;

                if (fsoBPK.ForeignID.HasValue && fsBpks.ContainsKey(fsoBPK.ForeignID.Value))
                {
                    var fsBPK = fsBpks[fsoBPK.ForeignID.Value];

                    if ((fsoBPK.BpkPrivat ?? "") != (fsBPK.BpkPrivat ?? ""))
                    {
                        Console.WriteLine($"ID={fsoBPK.ID} PersonBPKID={fsBPK.ID} different bpk " +
                            $"'{fsoBPK.Vorname}' != '{fsBPK.Vorname}'");
                        bpkErrCount++;
                    }
                }
                else
                {
                    Console.WriteLine($"ID={fsoBPK.ID} PersonBPKID={fsoBPK.ForeignID} not found.");

                    //using (var mdb = info.CreateOpenMssqlConnection())
                    //{
                    //    mdb.Execute(
                    //        Resources.InsertSyncJobMSSQL,
                    //        new
                    //        {
                    //            ID = fsoBPK.ID,
                    //            model = "res.partner.bpk",
                    //            sosyncWriteDate = fsoBPK.sosync_write_date,
                    //            system = SosyncSystem.FSOnline,
                    //            jobSourceFields = "{ \"info\": \"Manual re-sync for missing BPK by MKA\" }"
                    //        });
                    //}

                    notFoundCount++;
                }
            }

            if (bpkErrCount > 0 || notFoundCount > 0)
                _checkBpk.Add(info.Instance, new Tuple<int, int>(bpkErrCount, notFoundCount));
        }

        private static Dictionary<string, List<int>> _missingEmails = new Dictionary<string, List<int>>();
        private static Dictionary<string, List<int>> _emailsNotSync = new Dictionary<string, List<int>>();
        private static Dictionary<string, Tuple<int, int>> _checkEmail = new Dictionary<string, Tuple<int, int>>();
        private static void CheckEmails(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.Pillar.HostSosync != "sosync2")
                return;

            Dictionary<int, PersonEmail> fsoListe = null;
            Dictionary<int, PersonEmail> fsListe = null;

            using (var onlineCon = info.CreateOpenNpgsqlConnection())
            {
                fsoListe = onlineCon
                    .Query<PersonEmail>(
                        @"select 
                            id ID
                            ,sosync_fs_id ForeignID
                            ,sosync_write_date SosyncWriteDate
                            ,email Email
                        from
                            frst_personemail")
                    .ToDictionary(x => x.ID);
            }

            using (var fsCon = info.CreateOpenMssqlConnection())
            {
                fsListe = fsCon
                    .Query<PersonEmail>(
                        @"select
                            PersonEmailID ID
                            ,sosync_fso_id ForeignID
                            ,sosync_write_date SosyncWriteDate
                            ,case when isnull(EmailVor, '') + isnull(EmailNach, '') <> '' then isnull(EmailVor, '') + '@' + isnull(EmailNach, '') else null end Email
                        from
                            dbo.PersonEmail")
                    .ToDictionary(x => x.ID);
            }

            // Compare data

            var errCount = 0;
            var notFoundCount = 0;

            foreach (var fsModelKvp in fsListe)
            {
                var fsModel = fsModelKvp.Value;

                if (fsModel.ForeignID.HasValue && !fsoListe.ContainsKey(fsModel.ForeignID.Value))
                {
                    Console.WriteLine($"dbo.PersonEmail {fsModel.ID} has sosync_fso_id {fsModel.ForeignID} but it was not found in frst.personemail");
                }
            }

            foreach (var fsoModelKvp in fsoListe)
            {
                var fsoModel = fsoModelKvp.Value;

                if (fsoModel.ForeignID.HasValue && fsListe.ContainsKey(fsoModel.ForeignID.Value))
                {
                    var fsModel = fsListe[fsoModel.ForeignID.Value];

                    if ((fsoModel.Email ?? "").ToLower().Trim() != (fsModel.Email ?? "").ToLower().Trim())
                    {
                        if (!_emailsNotSync.ContainsKey(info.Instance))
                            _emailsNotSync.Add(info.Instance, new List<int>());

                        _emailsNotSync[info.Instance].Add(fsModel.ID);

                        errCount++;
                        Console.WriteLine($"Data mismatch: id={fsoModel.ID} PersonEmailID={fsModel.ID} ({fsoModel.Email} != {fsModel.Email})");
                        Console.WriteLine($"               online={fsoModel.SosyncWriteDate} studio={fsModel.SosyncWriteDate}");
                    }
                }
                else
                {
                    if (!_missingEmails.ContainsKey(info.Instance))
                        _missingEmails.Add(info.Instance, new List<int>());

                    _missingEmails[info.Instance].Add(fsoModel.ID);

                    // Console.WriteLine($"ID={fsoModel.ID} PersonID={fsoModel.ForeignID} ({fsoModel.Vorname} {fsoModel.Nachname}) not found.");
                    notFoundCount++;
                }
            }

            if (notFoundCount > 0)
                _checkEmail.Add(info.Instance, new Tuple<int, int>(notFoundCount, errCount));
        }

        private static Dictionary<string, List<int>> _modelNotSync = new Dictionary<string, List<int>>();
        private static Dictionary<string, List<int>> _modelNotSyncFSO = new Dictionary<string, List<int>>();
        private static Dictionary<string, List<int>> _missingModels = new Dictionary<string, List<int>>();
        private static Dictionary<string, Tuple<int, int>> _checkModel = new Dictionary<string, Tuple<int, int>>();
        private static void CheckModel(InstanceInfo info, Action<float> reportProgress, string studioModel, string onlineModel, string dataMSSQL, string dataPostgresSQL)
        {
            if (info.Pillar.HostSosync != "sosync2")
                return;

            Dictionary<int, CheckModel> fsoListe = null;
            Dictionary<int, CheckModel> fsListe = null;

            using (var onlineCon = info.CreateOpenNpgsqlConnection())
            {
                fsoListe = onlineCon
                    .Query<CheckModel>($@"
select 
    id as ID
    , sosync_fs_id as ForeignID
    , sosync_write_date as SosyncWriteDate
    , {dataPostgresSQL} as Data
from
    {onlineModel.Replace(".", "_")}
                        ")
                    .ToDictionary(x => x.ID);
            }

            using (var fsCon = info.CreateOpenMssqlConnection())
            {
                fsListe = fsCon
                    .Query<CheckModel>($@"
select
    {(studioModel.StartsWith("dbo.Aktion") ? "AktionsID" : studioModel.Split(".")[1] + "ID")} ID
    , sosync_fso_id ForeignID
    ,sosync_write_date SosyncWriteDate
    ,{dataMSSQL} Data
from
    {studioModel}
                        ")
                    .ToDictionary(x => x.ID);
            }

            // Compare data

            var errCount = 0;
            var notFoundCount = 0;

            var mssqlIDs = new List<int>();

            foreach (var fsModelKvp in fsListe)
            {
                var fsModel = fsModelKvp.Value;

                if (fsModel.ForeignID.HasValue && !fsoListe.ContainsKey(fsModel.ForeignID.Value))
                {
                    Console.WriteLine($"{studioModel} {fsModel.ID} has sosync_fso_id {fsModel.ForeignID} but it was not found in {onlineModel}");
                }
            }

            foreach (var fsoModelKvp in fsoListe)
            {
                var fsoModel = fsoModelKvp.Value;

                if (fsoModel.ForeignID.HasValue && fsListe.ContainsKey(fsoModel.ForeignID.Value))
                {
                    var fsModel = fsListe[fsoModel.ForeignID.Value];

                    if ((fsoModel.Data ?? "").ToLower().Trim() != (fsModel.Data ?? "").ToLower().Trim())
                    {
                        if (!_modelNotSync.ContainsKey(info.Instance))
                            _modelNotSync.Add(info.Instance, new List<int>());

                        _modelNotSync[info.Instance].Add(fsModel.ID);

                        if (!_modelNotSyncFSO.ContainsKey(info.Instance))
                            _modelNotSyncFSO.Add(info.Instance, new List<int>());

                        if (fsoModel != null && fsoModel.ID > 0)
                            _modelNotSyncFSO[info.Instance].Add(fsoModel.ID);

                        mssqlIDs.Add(fsModel.ID);

                        errCount++;
                        Console.WriteLine($"Data mismatch: id={fsoModel.ID} {studioModel}ID={fsModel.ID} ({fsoModel.Data} != {fsModel.Data})");
                        Console.WriteLine($"               online={fsoModel.SosyncWriteDate} studio={fsModel.SosyncWriteDate}");
                    }
                }
                else
                {
                    if (!_missingModels.ContainsKey(info.Instance))
                        _missingModels.Add(info.Instance, new List<int>());

                    _missingModels[info.Instance].Add(fsoModel.ID);

                    // Console.WriteLine($"ID={fsoModel.ID} PersonID={fsoModel.ForeignID} ({fsoModel.Vorname} {fsoModel.Nachname}) not found.");
                    notFoundCount++;
                }
            }


            // Test for first row
            //ForceFsoSyncModels("res.partner.donation_report", info.CreateAuthenticatedOdooClient(), info.CreateOpenNpgsqlConnection(), new[] { _modelNotSyncFSO[info.Instance].First() });

            // Process all
            //ForceFsoSyncModels("res.partner.donation_report", info.CreateAuthenticatedOdooClient(), info.CreateOpenNpgsqlConnection(), _modelNotSyncFSO[info.Instance].ToArray());

            //FsSyncModels(info, studioModel, (studioModel.StartsWith("Aktion") ? "AktionsID" : studioModel.Replace("dbo.", "") + "ID"), mssqlIDs, "Update wegen DSGVO-Loeschung");

            if (notFoundCount > 0)
                _checkModel.Add(info.Instance, new Tuple<int, int>(notFoundCount, errCount));
        }


        private static void FsSyncModels(InstanceInfo info, string model, string modelID, IEnumerable<int> ids, string infoText)
        {
            var query = Properties.Resources.InsertSyncJobMSSQLBatch;
            query = query
                .Replace("%MODEL%", model)
                .Replace("%MODELID%", modelID)
                .Replace("%ID-LIST%", string.Join(", ", ids))
                .Replace("%INFO%", infoText);
            
            using (var con = info.CreateOpenMssqlConnection())
            {
                con.Query(query);
            }
        }

        private static Dictionary<string, List<int>> _missingPartnerIDs = new Dictionary<string, List<int>>();
        private static Dictionary<string, List<int>> _PersonIDsNotUpToDate = new Dictionary<string, List<int>>();
        private static Dictionary<string, Tuple<int, int>> _checkPartner = new Dictionary<string, Tuple<int, int>>();
        private static void CheckPartners(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.Pillar.HostSosync != "sosync2")
                return;

            Dictionary<int, Partner> fsoListe = null;
            Dictionary<int, Partner> fsListe = null;

            using (var onlineCon = info.CreateOpenNpgsqlConnection())
            {
                fsoListe = onlineCon
                    .Query<Partner>(
                        @"select 
                            id ID
                            ,sosync_fs_id ForeignID
                            ,firstname Vorname
                            ,lastname Nachname
                            ,birthdate_web Geburtsdatum
                            ,street Strasse
                            ,street_number_web Hausnr
                            ,zip Plz
                            ,city Ort
                        from
                            res_partner")
                    .ToDictionary(x => x.ID);
            }

            using (var fsCon = info.CreateOpenMssqlConnection())
            {
                fsListe = fsCon
                    .Query<Partner>(
                        @"select
                            Person.PersonID ID
                            ,Person.sosync_fso_id ForeignID
                            ,Vorname
                            ,Name Nachname
                            ,Geburtsdatum
                            ,PersonAdresse.Strasse
                            ,PersonAdresse.Hausnummer
                            ,PersonAdresse.PLZ
                            ,PersonAdresse.Ort
                        from
                            dbo.Person
                            left join dbo.PersonAdresse
                                on Person.PersonID = PersonAdresse.PersonID
                                and Person.sosync_fso_id = PersonAdresse.sosync_fso_id")
                    .ToDictionary(x => x.ID);
            }

            // Compare data

            var errCount = 0;
            var notFoundCount = 0;

            foreach (var fsoModelKvp in fsoListe)
            {
                var fsoModel = fsoModelKvp.Value;

                if (fsoModel.ForeignID.HasValue && fsListe.ContainsKey(fsoModel.ForeignID.Value))
                {
                    var fsModel = fsListe[fsoModel.ForeignID.Value];

                    if ((fsoModel.Nachname ?? "").Trim() != (fsModel.Nachname ?? "").Trim()
                        || (fsoModel.Vorname ?? "").Trim() != (fsModel.Vorname ?? "").Trim()
                        || fsoModel.Geburtsdatum != fsModel.Geburtsdatum
                        || (fsoModel.Strasse ?? "").Trim() != (fsoModel.Strasse ?? "").Trim()
                        || (fsoModel.Hausnr ?? "").Trim() != (fsoModel.Hausnr ?? "").Trim()
                        || (fsoModel.Plz ?? "").Trim() != (fsoModel.Plz ?? "").Trim()
                        || (fsoModel.Ort ?? "").Trim() != (fsoModel.Ort ?? "").Trim()
                        )
                    {
                        if (!_PersonIDsNotUpToDate.ContainsKey(info.Instance))
                            _PersonIDsNotUpToDate.Add(info.Instance, new List<int>());

                        _PersonIDsNotUpToDate[info.Instance].Add(fsModel.ID);

                        errCount++;
                        Console.WriteLine($"Data mismatch: id={fsoModel.ID} PersonID={fsModel.ID}");
                    }
                }
                else
                {
                    if (fsoModel.ForeignID.HasValue)
                    {
                        if (!_missingPartnerIDs.ContainsKey(info.Instance))
                            _missingPartnerIDs.Add(info.Instance, new List<int>());

                        _missingPartnerIDs[info.Instance].Add(fsoModel.ForeignID.Value);
                    }

                    // Console.WriteLine($"ID={fsoModel.ID} PersonID={fsoModel.ForeignID} ({fsoModel.Vorname} {fsoModel.Nachname}) not found.");
                    notFoundCount++;
                }
            }

            if (notFoundCount > 0)
                _checkPartner.Add(info.Instance, new Tuple<int, int>(notFoundCount, errCount));
        }

        private static Dictionary<string, string> _checkDonationReport = new Dictionary<string, string>();
        private static void CheckDonationReports(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.Pillar.HostSosync != "sosync2")
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

            if (info.Pillar.HostSosync == "sosync2")
                CheckSosync2Donations(info, reportProgress, out fsoDonations, out fsDonations);
            else if (info.Pillar.HostSosync == "sosync1")
                CheckSosync1Donations(info, reportProgress, out fsoDonations, out fsDonations);

            var rpc = info.CreateAuthenticatedOdooClient();

            // Compare results

            var toUpdate = new List<int>();
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
                                toUpdate.Add(item.Key);
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
                var debug1 = string.Join(", ", toUpdate);

                foreach (var item in fsoDonations)
                {
                    var fsoItem = item.Value;
                    DonationReport fsItem = null;

                    if (fsDonations.ContainsKey(item.Value.ForeignID))
                        fsItem = fsDonations[item.Value.ForeignID];

                    if (fsItem == null)
                    {
                        missingCount++;
                    }
                }

                Console.WriteLine($"Differences: {diffCount} {(missingCount > 0 ? $"Missing: {missingCount}" : "")} ({info.Pillar.HostSosync})");

                if (diffCount > 0 || missingCount > 0)
                    _checkDonationReport.Add(info.Instance, $"{diffCount} {(missingCount > 0 ? $"Missing: {missingCount}" : "")} ({info.Pillar.HostSosync})");
            }
        }

        private static void CheckSosync2Donations(
            InstanceInfo info,
            Action<float> reportProgress,
            out Dictionary<int, DonationReport> fsoDonations,
            out Dictionary<int, DonationReport> fsDonations)
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

        private static void ForceFsoSyncModels (string modelName, OdooClient rpc, NpgsqlConnection onlineCon, int[] ids)
        {
            var tableName = modelName.Replace(".", "_");

            var updateQuery = $@"
                update {tableName} set sosync_write_date = concat(
	                to_char(now() at time zone 'utc', 'YYYY-MM-DD')
	                ,'T'
	                ,to_char(now() at time zone 'utc', 'HH24:MI:SS.US')
	                ,'Z'
	                )
                where id in ({string.Join(", ", ids)})
                ";

            onlineCon.Execute(updateQuery);

            var query = $@"
                select id, sosync_fs_id, sosync_write_date from {tableName}
                where id in ({string.Join(", ", ids)})
                ";

            var models = onlineCon.Query(query);

            var debug = "";
            foreach (var model in models)
            {
                string dummy = $"{model.id}";
                try
                {
                    var id = rpc.CreateModel("sosync.job.queue", new
                    {
                        job_date = model.sosync_write_date ?? DateTime.UtcNow,
                        job_source_system = "fso",
                        job_source_model = modelName,
                        job_source_record_id = (int)model.id,
                        job_source_target_record_id = (int?)model.sosync_fs_id,
                        job_source_sosync_write_date = model.sosync_write_date,
                        job_source_fields = $"{{\"sosync_write_date\": \"{model.sosync_write_date}\"}}",
                        //submission_url = submissionUrl
                    });
                }
                catch (Exception ex)
                {
                    debug += "," + dummy + Environment.NewLine;
                }
            }

            if (debug != "")
            {
                var dummy2 = debug;
            }
        }

        private static void CheckBranch(InstanceInfo info, Action<float> reportProgress)
        {
            var desiredBranch = "v2";

            if (info.Pillar.SosyncBranch != desiredBranch)
                throw new Exception($"{info.Instance} is not on branch {desiredBranch}");
        }


        private static void InitialSyncPayments(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.Pillar.HostSosync != "sosync2")
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

        private static Dictionary<string, int> _errorJobs = new Dictionary<string, int>();
        private static void UpdateErrorJobs(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.Pillar.HostSosync != "sosync2")
                return;

            var skip = new string[] {
                "dev1"
                ,"dev2"
                ,"deve"
                ,"veme"
                ,"wuni"
            };

            if (skip.Contains(info.Instance))
            {
                Console.WriteLine($"Skipping {info.Instance}");
                return;
            }


            using (var db = info.CreateOpenSyncerNpgsqlConnection())
            {
                var query = "update sosync_job set job_state = 'error_retry' where job_date > now() - interval '10 days' and parent_job_id is null and job_state = 'error' and job_run_count < 10;";
                var count = db.Query<int>(query, commandTimeout: 120).SingleOrDefault();
                _errorJobs.Add(info.Instance, count);
                Console.WriteLine($"{info.Instance}: {count}");
            }
        }


        private struct ArchiveProgress
        {
            public double Jobs;
            public double Archive;
        }

        private static Dictionary<string, int> _jobCounts = new Dictionary<string, int>();

        private static void GetSyncJobCount(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.Pillar.HostSosync != "sosync2")
                return;

            using (var db = info.CreateOpenSyncerNpgsqlConnection())
            {
                var countJob = db.QuerySingle<Int32>("select count(*) from sosync_job;");
                var countJobArchive = db.QuerySingle<Int32>("select count(*) from sosync_job_archive;");

                _jobCounts.Add(info.Instance, countJob + countJobArchive);
            }
        }

        private static Dictionary<string, int> _archiveProgress = new Dictionary<string, int>();
        private static void GetArchiveProgress(InstanceInfo info, Action<float> reportProgress)
        {
            if (info.Pillar.HostSosync != "sosync2")
                return;

            var skip = new string[] {
                "dev1"
                ,"dev2"
                ,"deve"
                ,"veme"
                ,"wuni"
            };

            if (skip.Contains(info.Instance))
            {
                Console.WriteLine($"Skipping {info.Instance}");
                return;
            }

            using (var db = info.CreateOpenSyncerNpgsqlConnection())
            {
                var counts = db.QuerySingle<ArchiveProgress>(
                    "select (SELECT n_live_tup FROM pg_stat_all_tables WHERE relname = 'sosync_job') Jobs,(SELECT n_live_tup FROM pg_stat_all_tables WHERE relname = 'sosync_job_archive') Archive",
                    commandTimeout: 240);

                var percent = (int)(counts.Archive / (counts.Jobs + counts.Archive) * 100f);

                if (percent < 0)
                    percent = 0;

                _archiveProgress.Add(info.Instance, percent);
            }
        }

        private static void FixLostSyncJobs(InstanceInfo info, Action<float> reportProgress)
        {
            // Skip non-sosync2 and disabled instances
            if (info.Instance != "demo" && (info.Pillar.HostSosync != "sosync2" || info.Pillar.SosyncEnabled))
                return;

            // Skip these named instances, but notify
            var skip = new string[] {
                "dev1"
                ,"dev2"
                ,"deve"
                ,"demo"
                ,"dadi"
                ,"aiat" // Initial sync noch nicht durch?
            };

            if (skip.Contains(info.Instance))
            {
                Console.WriteLine($"Skipping {info.Instance}");
                return;
            }

            var models = new[] {
                "res.company",
                "email.template",
                "account.fiscalyear",
                "res.partner.bpk",
                "res.partner.donation_report",
                "res.partner",
                "res.partner.fstoken",
                "frst.personemail",
                "frst.personemailgruppe",
                "frst.persongruppe",
                "frst.zgruppedetail",
                "frst.zgruppe",
                "payment.acquirer",
                "payment.transaction",
                "product.payment_interval",
                "product.product",
                "product.template",
                "sale.order",
                "sale.order.line",
                "mail.mass_mailing.contact",
                "mail.mass_mailing.list"
             };

            if (info.Instance == "gl2k")
                models = models.Append("gl2k.garden").ToArray();

            using (var fso = info.CreateAuthenticatedOdooClient())
            {
                foreach (var model in models)
                {
                    HandleUnsynchronized(model, fso);
                    //HandleUpdated(model, fso);
                }
            }
        }

        private static void HandleUpdated(string model, OdooClient fso)
        {
            var args = new[]
            {
                new OdooSearchArgument("write_date", ">=", new DateTime(2019, 12, 10))
            };

            var foundIDs = fso.SearchBy(model, args);

            if (foundIDs.Length > 0)
            {
                var cnt = 0;
                foreach (var id in foundIDs)
                {
                    var data = fso.GetDictionary(model, id, new[] { "write_date", "sosync_write_date" });

                    var wd = OdooConvert.ToDateTime((string)data["write_date"], true);
                    var swd = OdooConvert.ToDateTime((string)data["sosync_write_date"], true);

                    var diff = wd - swd;

                    if (diff.HasValue && diff.Value.TotalSeconds > 60 * 5)
                    {
                        cnt++;
                        Console.WriteLine($"{id}: wd= {wd.Value.ToString("yyyy-MM-dd HH:mm:ss.fffffff")}\n{new string(' ', id.ToString().Length)}  swd={swd.Value.ToString("yyyy-MM-dd HH:mm:ss.fffffff")}");

                        fso.UpdateModel(model, new { sosync_write_date = wd }, id);

                        var jobID = fso.CreateModel("sosync.job.queue", new
                        {
                            job_date = OdooConvert.ToDateTime((string)data["write_date"], true) ?? DateTime.UtcNow,
                            job_source_system = "fso",
                            job_source_model = model,
                            job_source_record_id = id,
                            job_source_target_record_id = (int?)null,
                            job_source_sosync_write_date = OdooConvert.ToDateTime((string)data["write_date"], true)
                        });
                    }
                }

                Console.WriteLine($"{model}: {cnt}");
            }
        }

        private static void HandleUnsynchronized(string model, OdooClient fso)
        {
            // Search for sosync_fs_id = null
            var args = new[]
            {
                new OdooSearchArgument("sosync_fs_id", "=", false)
            };

            var foundIDs = fso.SearchBy(model, args)
                .ToList();

            // Search for sosync_fs_id = 0
            args = new[]
            {
                new OdooSearchArgument("sosync_fs_id", "=", 0)
            };

            foundIDs.AddRange(fso.SearchBy(model, args));

            if (foundIDs.Count > 0)
            {
                Console.Write($"{model}: {foundIDs.Count} missing ");

                var i = 0;
                foreach (var id in foundIDs)
                {
                    i++;

                    if (((int)(i / (float)foundIDs.Count * 100f)) % 10 == 0)
                        Console.Write(".");

                    var data = fso.GetDictionary(model, id, new[] { "sosync_write_date", });

                    var jobID = fso.CreateModel("sosync.job.queue", new
                    {
                        job_date = OdooConvert.ToDateTime((string)data["sosync_write_date"]) ?? DateTime.UtcNow,
                        job_source_system = "fso",
                        job_source_model = model,
                        job_source_record_id = id,
                        job_source_target_record_id = (int?)null,
                        job_source_sosync_write_date = OdooConvert.ToDateTime((string)data["sosync_write_date"])
                    });
                }

                Console.WriteLine(" done.");
            }
        }
    }
}
