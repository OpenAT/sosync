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
            IHostService svc = (IHostService)host.Services.GetService(typeof(IHostService));
            IConfiguration config = (IConfiguration)host.Services.GetService(typeof(IConfiguration));

            var sosyncConfig = (SosyncOptions)host.Services.GetService(typeof(SosyncOptions));

            log.LogInformation(String.Join(" ",
                $"Sosync started for instance {sosyncConfig.Instance},",
                $"running on {osNameAndVersion}",
                $"with end points {string.Join(", ", defaultUrls)}"));

            if (!string.IsNullOrEmpty(Program.LogFile))
                log.LogInformation($"Logging to: {Program.LogFile}");

            // Attach handler for the linux sigterm signal
            AssemblyLoadContext.Default.Unloading += (obj) => HandleSigTerm(host.Services, log, svc);

            try
            {
                if (!forceQuit && !string.IsNullOrEmpty(sosyncConfig.Instance))
                    SetupDb(sosyncConfig, log);
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
                    host.Run(svc.Token);
            }
            catch (IOException ex)
            {
                log.LogCritical(ex.Message);
            }

            log.LogInformation("Sosync service ended");
        }

        /// <summary>
        /// Handles the linux "sigterm" signal, to gracefully terminate.
        /// </summary>
        /// <param name="ioc">The dependency injection container.</param>
        /// <param name="log">The logger to be used for logging.</param>
        /// <param name="svc">The host service to request termination.</param>
        private static void HandleSigTerm(IServiceProvider ioc, ILogger<Program> log, IHostService svc)
        {
            IBackgroundJob<SyncWorker> job = (IBackgroundJob<SyncWorker>)ioc.GetService(typeof(Common.Interfaces.IBackgroundJob<SyncWorker>));
            IBackgroundJob<ProtocolWorker> protocolJob = (IBackgroundJob<ProtocolWorker>)ioc.GetService(typeof(Common.Interfaces.IBackgroundJob<ProtocolWorker>));
            log.LogInformation($"Process termination requested (job status: {job.Status}, protocol status: {protocolJob.Status})");

            job.ShutdownPending = true;
            protocolJob.ShutdownPending = true;

            // As logn the job status isn't stopped or error, keep requesting to stop it
            while (job.Status != BackgoundJobState.Idle && job.Status != BackgoundJobState.Error)
            {
                log.LogInformation("Asking jobs to stop");
                job.Stop();
                protocolJob.Stop();
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
        private static void SetupDb(SosyncOptions config, ILogger<Program> log)
        {
            log.LogInformation($"Ensure sosync database is up to date...");

            using (var db = new DataService(config))
            {
                db.Setup();
            }

            log.LogInformation($"Database check done.");
        }

        private static void SetSosyncDefaultConfig(SosyncOptions config, ILogger<Program> log)
        {
            var text = "not invalid. Setting it to";

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