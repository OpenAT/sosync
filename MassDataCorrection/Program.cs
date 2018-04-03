using Npgsql;
using Odoo;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Dapper;
using MassDataCorrection.Models;

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
                //processor.Process(CheckOpenSyncJobs, null );
                //processor.Process(FillNewDonationReportFields, new[] { "bsvw" });
                processor.Process(FillNewDonationReportFields, null);

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
    }
}
