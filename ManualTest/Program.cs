using dadi_data;
using dadi_data.Models;
using Odoo;
using Odoo.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ManualTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Started.");

            Stopwatch s = new Stopwatch();
            var client = new OdooClient($"http://wwfa.datadialog.net/xmlrpc/2/", "wwfa");
            client.Authenticate("sosync", "YWGXlaB5cfPUDs9a");

            int? b = null;

            s.Start();
            var company = client.GetModel<resCompany>("res.company", b.Value);
            s.Stop();

            Console.ReadKey();
        }

        private static void Threads()
        {
            // Give sosync2 server some time to start up
            Thread.Sleep(5000);

            Console.WriteLine("Manual testing...");

            //int count = 200; // 2000 Entries
            //var tasks = new Task[]
            //{
            //    StartRequestsAsync("A", count),
            //    StartRequestsAsync("B", count),
            //    StartRequestsAsync("C", count),
            //    StartRequestsAsync("D", count),
            //    StartRequestsAsync("E", count),
            //    StartRequestsAsync("F", count),
            //    StartRequestsAsync("G", count),
            //    StartRequestsAsync("H", count),
            //    StartRequestsAsync("I", count),
            //    StartRequestsAsync("J", count)
            //};

            int threadCount = 5;
            int count = 100; // 5 x 100 = 500

            for (int ti = 0; ti < threadCount; ti++)
            {
                Thread t = new Thread(new ParameterizedThreadStart((p) => 
                {
                    var pr = (ThreadParam)p;
                    StartRequestsAsync(pr.ID, pr.Nr);
                }));

                t.Start(new ThreadParam() { ID = ((char)(65 + ti)).ToString(), Nr = count });
            }

            //StartRequestsAsync("A", count);
            //StartRequestsAsync("B", count);
            //StartRequestsAsync("C", count);
            //StartRequestsAsync("D", count);
            //StartRequestsAsync("E", count);
            //StartRequestsAsync("F", count);
            //StartRequestsAsync("G", count);
            //StartRequestsAsync("H", count);
            //StartRequestsAsync("I", count);
            //StartRequestsAsync("J", count);

            //Task.WhenAll(tasks);

            Console.WriteLine($"All tasks finished.");
        }

        //static async Task StartRequestsAsync(string identifier, int count)
        //{
        //    //string template = "http://localhost:5050/job/create?job_date={0:yyyy-MM-dd}%20{0:HH:mm:ss.fffffff}&job_source_system=fs&job_source_model=dbo.Person&job_source_record_id=13&job_source_sosync_write_date={0:yyyy-MM-dd}T{0:HH:mm:ss.fffffff}Z";

        //    var requests = new List<Task>(count);

        //    for (int i = 0; i < count; i++)
        //    {
        //        var myId = identifier;
        //        var myNr = i + 1;

        //        var t = Task.Run(() =>
        //        {
        //            UpdateMssql(myId, myNr);
        //        });

        //        requests.Add(t);

        //        // requests.Add(Task.Run(() => UpdateMssql(identifier, i + 1)));
        //        //requests.Add(factory.StartNew(() => UpdateMssql(identifier, i + 1)));

        //        //var req = HttpWebRequest.Create(String.Format(template, DateTime.UtcNow));
        //        //req.Method = "GET";
        //        //requests.Add(req.GetResponseAsync());
        //    }
        //    await Task.WhenAll(requests);
        //}

        static void StartRequestsAsync(string identifier, int count)
        {
            //string template = "http://localhost:5050/job/create?job_date={0:yyyy-MM-dd}%20{0:HH:mm:ss.fffffff}&job_source_system=fs&job_source_model=dbo.Person&job_source_record_id=13&job_source_sosync_write_date={0:yyyy-MM-dd}T{0:HH:mm:ss.fffffff}Z";

            //var requests = new List<Task>(count);

            using (var db = new DataService<dboPerson>("Data Source=MSSQL1;Initial Catalog=mdb_demo; Integrated Security=True;"))
            {
                for (int i = 0; i < count; i++)
                {
                    UpdateMssql(identifier, i + 1, db);

                    //var t = Task.Run(() =>
                    //{
                    //    UpdateMssql(myId, myNr);
                    //});

                    //requests.Add(t);

                    // requests.Add(Task.Run(() => UpdateMssql(identifier, i + 1)));
                    //requests.Add(factory.StartNew(() => UpdateMssql(identifier, i + 1)));

                    //var req = HttpWebRequest.Create(String.Format(template, DateTime.UtcNow));
                    //req.Method = "GET";
                    //requests.Add(req.GetResponseAsync());
                }
            }
            //await Task.WhenAll(requests);
        }


        private static Random _rnd = new Random();

        private static void UpdateMssql(string id, int nr, DataService<dboPerson> db)
        {
            Console.WriteLine($"{id}: Updating mssql {nr}...");

            try
            {
                var pers = db.Read(new { PersonID = 13 }).SingleOrDefault();
                pers.Name = $"Reynoldse-{id}-{nr}";
                db.Update(pers);

                Console.WriteLine($"{id}: {nr} is done...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{id}: {nr} error: {ex.Message}");
            }
        }
    }
}