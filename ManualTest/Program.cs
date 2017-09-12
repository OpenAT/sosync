using dadi_data;
using dadi_data.Models;
using System;
using System.Collections.Generic;
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
            // Give sosync2 server some time to start up
            Thread.Sleep(5000);

            Console.WriteLine("Manual testing...");

            int count = 100;
            var tasks = new Task[]
            {
                StartRequestsAsync("A", count),
                StartRequestsAsync("B", count),
                StartRequestsAsync("C", count),
                StartRequestsAsync("D", count),
                StartRequestsAsync("E", count)
            };

            Task.WaitAll(tasks);

            Console.WriteLine($"All tasks finished.");
            Console.ReadKey();
        }

        static async Task StartRequestsAsync(string identifier, int count)
        {
            //string template = "http://localhost:5050/job/create?job_date={0:yyyy-MM-dd}%20{0:HH:mm:ss.fffffff}&job_source_system=fs&job_source_model=dbo.Person&job_source_record_id=13&job_source_sosync_write_date={0:yyyy-MM-dd}T{0:HH:mm:ss.fffffff}Z";

            var requests = new List<Task>(count);

            for (int i = 0; i < count; i++)
            {
                var myId = identifier;
                var myNr = i + 1;

                var t = Task.Run(() =>
                {
                    UpdateMssql(myId, myNr);
                });

                requests.Add(t);
                
                // requests.Add(Task.Run(() => UpdateMssql(identifier, i + 1)));
                //requests.Add(factory.StartNew(() => UpdateMssql(identifier, i + 1)));

                //var req = HttpWebRequest.Create(String.Format(template, DateTime.UtcNow));
                //req.Method = "GET";
                //requests.Add(req.GetResponseAsync());
            }
            await Task.WhenAll(requests);
        }

        private static Random _rnd = new Random();

        private static void UpdateMssql(string id, int nr)
        {
            Console.WriteLine($"{id}: Updating mssql {nr}...");

            try
            {
                using (var db = new DataService<dboPerson>("Data Source=MSSQL1;Initial Catalog=mdb_demo; Integrated Security=True;"))
                {
                    var pers = db.Read(new { PersonID = 13 }).SingleOrDefault();
                    pers.Name = $"Reynoldse-{id}-{nr}";
                    db.Update(pers);
                }

                Console.WriteLine($"{id}: {nr} is done...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{id}: {nr} error: {ex.Message}");
            }
        }
    }
}