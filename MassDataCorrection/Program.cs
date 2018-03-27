using Npgsql;
using Odoo;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

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
                //processor.Process(CheckOpenSyncJobs, new[] { "diaf" });
                processor.Process(CheckOpenSyncJobs, null);

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

            var odoo = info.CreateAuthenticatedOdooClient();

            using (var con = info.CreateOpenMssqlConnection())
            {
                reportProgress(1f);

                //for (float i = 0f; i <= 1f; i += 0.01f)
                //{
                //    System.Threading.Thread.Sleep(10);
                //    reportProgress(i);
                //}
            }
        }
    }
}
