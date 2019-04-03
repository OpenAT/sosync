using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Syncer.Services;
using Syncer.Workers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using WebSosync.Common.Enumerations;
using WebSosync.Common.Interfaces;
using WebSosync.Data;
using WebSosync.Data.Models;
using WebSosync.Interfaces;

namespace WebSosync
{
    public class Program
    {
        #region Members
        public static string[] Args { get; private set; }
        public static string ConfigurationINI { get; private set; }
        public static string LogFile { get; set; }
        public static Dictionary<string, string> SwitchMappings { get; private set; }
        private static ILogger<Program> _log;
        #endregion

        #region Class initializers
        static Program()
        {
            SwitchMappings = new Dictionary<string, string>();

            // Short switch for configuration parameter
            SwitchMappings.Add("-c", "conf");

            // All the properties of the sosync configuration class are
            // valid command line switches. But in order to skip the
            // section-notation a switch map is defined for each property
            var properties = typeof(SosyncOptions).GetProperties();

            foreach (var prop in properties)
            {
                var propName = prop.Name.ToLower();
                SwitchMappings.Add($"--{propName}", $"sosync:{propName}");
            }
        }
        #endregion

        #region Methods
        public static void Main(string[] args)
        {
            Args = args;

            var osNameAndVersion = RuntimeInformation.OSDescription;
            bool forceQuit = false;

            var parameters = new ConfigurationBuilder()
                .AddCommandLine(Program.Args, Program.SwitchMappings)
                .Build();

            // Conf is the only hard-coded parameter, since it is
            // required to specify the configuration INI file
            Program.ConfigurationINI = parameters["conf"];

            // Without the configuarion INI, print message and quit.
            // Logging is not possible at this point, hence the console
            // Message.
            if (string.IsNullOrEmpty(Program.ConfigurationINI))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("Fail: ");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("Parameter \"--conf\" required.");
                return;
            }

            // Now load the actual configuration from the INI file.
            // The command line is added, because command line
            // overrides INI configuration
            var kestrelConfig = new ConfigurationBuilder()
                .AddIniFile(Program.ConfigurationINI)
                .AddCommandLine(Program.Args, Program.SwitchMappings)
                .Build();

            var defaultUrls = NetworkInterface.GetAllNetworkInterfaces()
                .Where(x => x.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                .SelectMany(x => x.GetIPProperties().UnicastAddresses)
                .Select(x => $"http://{x.Address}:{kestrelConfig["sosync:port"]}")
                .ToList();

            defaultUrls.Insert(0, $"http://localhost:{kestrelConfig["sosync:port"]}");

#warning TODO: Implement it, so that INI configuration overrides default behaviour

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .UseUrls(defaultUrls.ToArray())
                .Build();

            ILogger<Program> log = (ILogger<Program>)host.Services.GetService(typeof(ILogger<Program>));
            _log = log;

            IHostService svc = (IHostService)host.Services.GetService(typeof(IHostService));
            IConfiguration config = (IConfiguration)host.Services.GetService(typeof(IConfiguration));

            var sosyncConfig = (SosyncOptions)host.Services.GetService(typeof(SosyncOptions));

            // Register unhandled exception handler
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            log.LogInformation(String.Join(" ",
                $"Sosync started for instance {sosyncConfig.Instance},",
                $"running on {osNameAndVersion}",
                $"with end points {string.Join(", ", defaultUrls)}"));

            if (!string.IsNullOrEmpty(Program.LogFile))
                log.LogInformation($"Logging to: {Program.LogFile}");

            // Check max thread parameter and set default value
            if (!sosyncConfig.Max_Threads.HasValue)
                sosyncConfig.Max_Threads = 2;

            // Job package size default value
            if (!sosyncConfig.Job_Package_Size.HasValue)
                sosyncConfig.Job_Package_Size = 20;

            // Model lock timeout default value
            if (!sosyncConfig.Model_Lock_Timeout.HasValue)
                sosyncConfig.Model_Lock_Timeout = 10000;

            // Active check: quit if not active
            if (!sosyncConfig.Active.HasValue)
                sosyncConfig.Active = true;

            if (!sosyncConfig.Active.Value)
            {
                log.LogWarning($"INI parameter: active = false, process ending.");
                return;
            }

            // Attach handler for the linux sigterm signal
            AssemblyLoadContext.Default.Unloading += (obj) => HandleSigTerm(host.Services, log, svc);

            try
            {
                if (!forceQuit && !string.IsNullOrEmpty(sosyncConfig.Instance))
                    TestDatabaseConnection(sosyncConfig, log);
            }
            catch (PostgresException ex)
            {
                // Log the exception and exit the program
                log.LogError(ex.Message);
                forceQuit = true;
            }
            catch (SocketException)
            {
                // Log the exception and exit the program
                log.LogError("Could not connect to pgSQL server.");
                forceQuit = true;
            }

            try
            {
                if(!forceQuit)
                {
                    var flowSvc = (FlowService)host.Services.GetService(typeof(FlowService));
                    flowSvc.ThrowOnMissingFlowAttributes();
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                forceQuit = true;
            }

            TimeService timeSvc = null;
            try
            {
                timeSvc = (TimeService)host.Services.GetService(typeof(TimeService));
                timeSvc.ThrowOnTimeDrift();
            }
            catch (Exception ex)
            {
                log.LogWarning(ex.Message);
            }
            finally
            {
                if (timeSvc != null)
                    timeSvc.LastDriftCheck = null; // Don't consider the initial time check
            }

            SetSosyncDefaultConfig(sosyncConfig, log);

            try
            {
                // Start the webserver if there is no forced quit
                if (!forceQuit)
                {
                    var jobWorker = (IBackgroundJob<SyncWorker>)host.Services.GetService(typeof(IBackgroundJob<SyncWorker>));
                    jobWorker.Start();

                    host.Run();
                    //host.Run(svc.Token);
                }
            }
            catch (IOException ex)
            {
                log.LogCritical(ex.Message);
            }

            log.LogInformation("Sosync service ended");
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                _log.LogError(e.ExceptionObject.ToString());
            }
            catch (Exception ex)
            {
                // Logger was not ready yet, so print out both errors for a chance to see them on a console
                Console.WriteLine("------------------------");
                Console.WriteLine(e.ExceptionObject.ToString());
                Console.WriteLine("------------------------");
                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// Handles the linux "sigterm" signal, to gracefully terminate.
        /// </summary>
        /// <param name="ioc">The dependency injection container.</param>
        /// <param name="log">The logger to be used for logging.</param>
        /// <param name="svc">The host service to request termination.</param>
        private static void HandleSigTerm(IServiceProvider ioc, ILogger<Program> log, IHostService svc)
        {
            log.LogWarning($"SigTerm received: ico={(ioc == null ? "<NULL>" : "<INSTANCE>")}");
            log.LogWarning($"                  log={(log == null ? "<NULL>" : "<INSTANCE>")}");
            log.LogWarning($"                  svc={(svc == null ? "<NULL>" : "<INSTANCE>")}");

            IBackgroundJob<SyncWorker> job = (IBackgroundJob<SyncWorker>)ioc.GetService(typeof(Common.Interfaces.IBackgroundJob<SyncWorker>));
            IWebHost host = (IWebHost)ioc.GetService(typeof(IWebHost));
            log.LogInformation($"Process termination requested (job status: {job.Status})");

            log.LogWarning($"                  job={(job == null ? "<NULL>" : "<INSTANCE>")}");
            log.LogWarning($"                  host={(host == null ? "<NULL>" : "<INSTANCE>")}");

            job.ShutdownPending = true;

            // As logn the job status isn't stopped or error, keep requesting to stop it
            while (job.Status != BackgoundJobState.Idle && job.Status != BackgoundJobState.Error)
            {
                log.LogInformation("Asking jobs to stop");
                job.Stop();
                System.Threading.Thread.Sleep(1000);
            }

            // The job terminated cleanly, graceful exit by requesting the host thread to shut down
            log.LogInformation($"Exiting gracefully.");

            host.StopAsync();
            svc.RequestShutdown();
        }

        /// <summary>
        /// Connects to the database, to check availability.
        /// </summary>
        /// <param name="config">The configuration to be used to read the database connection details.</param>
        /// <param name="log">The Logger to be used foir logging.</param>
        private static void TestDatabaseConnection(SosyncOptions config, ILogger<Program> log)
        {
            log.LogInformation($"Testing connection to database...");

            using (var db = new DataService(config))
            { }

            log.LogInformation($"Database connection successful.");
        }

        private static void SetSosyncDefaultConfig(SosyncOptions config, ILogger<Program> log)
        {
            var text = " is invalid. Setting it to";

            if (config.Throttle_ms < 0)
            {
                config.Throttle_ms = 0;
                log.LogInformation($"{nameof(config.Throttle_ms)} {text} {config.Throttle_ms}");
            }

            if (config.Protocol_Throttle_ms < 0)
            {
                config.Protocol_Throttle_ms = 0;
                log.LogInformation($"{nameof(config.Protocol_Throttle_ms)} {text} {config.Protocol_Throttle_ms}");
            }

            if (config.Max_Time_Drift_ms <= 0)
            {
                config.Max_Time_Drift_ms = 50;
                log.LogInformation($"{nameof(config.Max_Time_Drift_ms)} {text} {config.Max_Time_Drift_ms}");
            }
        }
        #endregion
    }
}