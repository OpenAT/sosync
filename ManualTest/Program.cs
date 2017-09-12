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

            int count = 20;
            var tasks = new Task[]
            {
                StartRequestsAsync("A", count),
                StartRequestsAsync("B", count),
                StartRequestsAsync("C", count),
                StartRequestsAsync("D", count),
                StartRequestsAsync("E", count)
            };

            Task.WhenAll(tasks);

            Console.WriteLine("Done!");
            Console.ReadKey();
        }

        static async Task StartRequestsAsync(string identifier, int count)
        {
            string template = "http://localhost:5050/job/create?job_date={0:yyyy-MM-dd}%20{0:HH:mm:ss.fffffff}&job_source_system=fs&job_source_model=dbo.Person&job_source_record_id=13&job_source_sosync_write_date={0:yyyy-MM-dd}T{0:HH:mm:ss.fffffff}Z";

            var requests = new List<Task<WebResponse>>(count);

            for (int i = 0; i < count; i++)
            {
                using (var db = new DataService<dboPerson>("Data Source=MSSQL1;Initial Catalog=mdb_demo; Integrated Security=True;"))
                {
                    db.BeginTransaction();
                    var pers = db.Read(new { PersonID = 13 }).SingleOrDefault();
                    pers.Name = $"Reynoldse-{identifier}-{i + 1}";
                    db.Update(pers);
                    db.CommitTransaction();
                }

                Console.WriteLine($"{identifier}: Sending request {i + 1}...");
                var req = HttpWebRequest.Create(String.Format(template, DateTime.UtcNow));
                req.Method = "GET";
                requests.Add(req.GetResponseAsync());
                await Task.WhenAll(requests);
            }
        }
    }
}