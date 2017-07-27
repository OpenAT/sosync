using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace ManualTest
{
    class Program
    {
        static void Main(string[] args)
        {

            Console.WriteLine("Manual testing...");
            var t = StartRequestsAsync();
            t.Wait();
            Console.WriteLine("Done!");
        }

        static async Task StartRequestsAsync()
        {
            var count = 100;
            string template = "http://localhost:5050/job/create?job_date={0:yyyy-MM-dd}%20{0:HH:mm:ss.fff}&job_source_system=fso&job_source_model=res.company&job_source_record_id=1";

            var requests = new List<Task<WebResponse>>(count);

            for (int i = 0; i < count; i++)
            {
                //Console.WriteLine(String.Format(template, DateTime.UtcNow));
                Console.WriteLine($"Sending request {i + 1}...");
                var req = HttpWebRequest.Create(String.Format(template, DateTime.UtcNow));
                req.Method = "GET";
                requests.Add(req.GetResponseAsync());
                await Task.WhenAll(requests);
            }
        }
    }
}