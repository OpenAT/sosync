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
                //processor.Process(CheckFaultyPhoneNumbers, null);
                //processor.Process(CheckOpenSyncJobs, null);
                //processor.Process(FillNewDonationReportFields, new[] { "bsvw" });
                //processor.Process(FillNewDonationReportFields, null);
                //processor.Process(FillMissingPartnerBPKFields, new[] { "demo" });
                //processor.Process(FillMissingPartnerBPKFields, null);
                //processor.Process(DeleteAndResyncFiscalYears, new[] { "demo" });
                //processor.Process(DeleteAndResyncFiscalYears, null);
                //processor.Process(CheckUnsyncedPartners, null);

                //processor.Process(CheckGroups, new[] { "npha" });
                processor.Process(CheckGroups, null);
                PrintGroupStats();

                //ShowUnsynchedPartners();

                var dummy = string.Join(Environment.NewLine, _tel.Select(x => $"{x.Key}\t{x.Value}"));

                Console.WriteLine("\nDone. Press any key to quit.");
            }
            else
            {
                Console.WriteLine("\nCancelled. Press any key to quit.");
            }

            Console.ReadKey();
        }

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
            catch (Exception ex)
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
            catch (Exception ex)
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
                var cmd = new NpgsqlCommand("select job_source_type, job_source_model, count(*) cnt from sync_table where job_state = 'new' group by job_source_type, job_source_model;", con);

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

                var data = msCon.Query<NlRow>(Resources.NewsletterQuery)
                    .ToList();

                var anzahl = 0;
                foreach (var row in data)
                {
                    // Console.WriteLine($"Creating SyncJob for \"res.partner\" {row.sosync_fso_id}");
                    // rpc.CreateSyncJob("res.partner", row.sosync_fso_id);
                    anzahl++;
                }

                if (!groupsStats.ContainsKey(info.Instance))
                    groupsStats.Add(info.Instance, anzahl);

                Console.WriteLine($"Anzahl: {anzahl}");
            }
        }

        private static void PrintGroupStats()
        {
            foreach (var item in groupsStats)
            {
                Console.WriteLine($"{item.Key}: {item.Value}");
            }
        }
    }
}
