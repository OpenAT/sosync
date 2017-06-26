using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using WebSosync.Data;
using WebSosync.Data.Models;
using WebSosync.Enumerations;
using WebSosync.Interfaces;

namespace WebSosync
{
    public class Program
    {
        #region Members
        public static string[] Args { get; private set; }
        public static string ConfigurationINI { get; private set; }
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

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .UseUrls($"http://localhost:{kestrelConfig["sosync:port"]}")
                .Build();

            ILogger<Program> log = (ILogger<Program>)host.Services.GetService(typeof(ILogger<Program>));
            IHostService svc = (IHostService)host.Services.GetService(typeof(IHostService));
            IConfiguration config = (IConfiguration)host.Services.GetService(typeof(IConfiguration));

            var sosyncConfig = (SosyncOptions)host.Services.GetService(typeof(SosyncOptions));
            
            // Attach handler for the linux sigterm signal
            AssemblyLoadContext.Default.Unloading += (obj) => HandleSigTerm(host.Services, log, svc);

            try
            {
                if (!forceQuit && !string.IsNullOrEmpty(sosyncConfig.Instance))
                {
                    log.LogInformation($"Running on {osNameAndVersion}");
                    log.LogInformation($"Instance name: {sosyncConfig.Instance}");

                    SetupDb(sosyncConfig, log);
                }
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
                // Start the webserver if there is no forced quit
                if (!forceQuit)
                    host.Run(svc.Token);
            }
            catch (IOException ex)
            {
                log.LogCritical(ex.Message);
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
        private static void SetupDb(SosyncOptions config, ILogger<Program> log)
        {
            log.LogInformation($"Setting up database");

            using (var db = new DataService(config))
            {
                db.Setup();
            }
        }
        #endregion
    }
}