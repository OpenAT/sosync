using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using WebSosync.Interfaces;
using WebSosync.Enumerations;
using Microsoft.Extensions.Configuration;
using WebSosync.Data;
using WebSosync.Data.Helpers;

namespace WebSosync
{
    public class Program
    {
        #region Methods
        public static void Main(string[] args)
        {
            Args = args;
            var osNameAndVersion = RuntimeInformation.OSDescription;

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                //.UseIISIntegration()
                .UseStartup<Startup>()
                //.UseApplicationInsights()
                .Build();

            ILogger<Program> log = (ILogger<Program>)host.Services.GetService(typeof(ILogger<Program>));
            IHostService svc = (IHostService)host.Services.GetService(typeof(IHostService));
            IConfiguration config = (IConfiguration)host.Services.GetService(typeof(IConfiguration));

            log.LogInformation($"Running on {osNameAndVersion}");
            log.LogInformation($"Instance name: {config["instance"]}");

            log.LogInformation($"Setting up database");
            using (var db = new DataService(ConnectionHelper.GetPostgresConnectionString(
                config["instance"],
                config["sosync_user"],
                config["sosync_pass"])))
            {
                db.Setup();
            }

            // Handle he linux sigterm signal
            AssemblyLoadContext.Default.Unloading += (obj) =>
            {
                IBackgroundJob job = (IBackgroundJob)host.Services.GetService(typeof(IBackgroundJob));
                log.LogInformation($"Process termination requested (job status: {job.Status})");
                job.ShutdownPending = true;

                // As logn the job status isn't stopped or error, keep requesting to stop it
                while (job.Status != ServiceState.Stopped && job.Status != ServiceState.Error)
                {
                    log.LogInformation("Asking job to stop");
                    job.Stop();
                    System.Threading.Thread.Sleep(1000);
                }

                // The job terminated cleanly, graceful exit by requesting the host thread to shut down
                log.LogInformation($"Exiting gracefully.");
                svc.RequestShutdown();
            };

            // Start the webserver
            host.Run(svc.Token);
        }
        #endregion

        #region Members
        public static string[] Args { get; private set; }
        #endregion
    }
}