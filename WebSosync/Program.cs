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
using System;

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

            SetupDb(config, log);

            // Handle he linux sigterm signal
            AssemblyLoadContext.Default.Unloading += (obj) => HandleSigTerm(host.Services, log, svc);

            // Start the webserver
            host.Run(svc.Token);
        }

        /// <summary>
        /// Handles the linux "sigterm" signal, to gracefully terminate.
        /// </summary>
        /// <param name="ioc">The dependency injection container.</param>
        /// <param name="log">The logger to be used for logging.</param>
        /// <param name="svc">The host service to request termination.</param>
        private static void HandleSigTerm(IServiceProvider ioc, ILogger<Program> log, IHostService svc)
        {
            IBackgroundJob job = (IBackgroundJob)ioc.GetService(typeof(IBackgroundJob));
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
        }

        /// <summary>
        /// Connects to the database and tries to create the sync table.
        /// </summary>
        /// <param name="config">The configuration to be used to read the database connection details.</param>
        /// <param name="log">The Logger to be used foir logging.</param>
        private static void SetupDb(IConfiguration config, ILogger<Program> log)
        {
            log.LogInformation($"Setting up database");

            using (var db = new DataService(ConnectionHelper.GetPostgresConnectionString(
                config["instance"],
                config["sosync_user"],
                config["sosync_pass"])))
            {
                db.Setup();
            }
        }
        #endregion

        #region Members
        public static string[] Args { get; private set; }
        #endregion
    }
}