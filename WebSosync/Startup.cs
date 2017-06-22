using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using WebSosync.Interfaces;
using System.Runtime.InteropServices;
using System.Reflection;
using Serilog;
using Serilog.Events;

namespace WebSosync
{
    public class Startup
    {
        #region Methods
        /// <summary>
        /// Handles the configuration for a web host.
        /// </summary>
        /// <param name="env">Provides access to the hosting environment.</param>
        public Startup(IHostingEnvironment env)
        {
            // Build separate configuration from command line
            var tempConfig = new ConfigurationBuilder()
                .AddCommandLine(Program.Args)
                .Build();

            if (string.IsNullOrEmpty(tempConfig["instance"]))
                throw new ArgumentException("Parameter \"instance\" required. Use --instance ****");

            // Build the INI path and filename, depending on the instance name
            var iniFile = $"{tempConfig["instance"]}_sosync.ini";

            string iniConfig;

            // In development, on windows, expect the INI file in the project folder
            // In production, expect the INI file inside the app folder
            if (env.IsDevelopment() && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                iniConfig = Path.Combine(env.ContentRootPath, iniFile);
            else
                iniConfig = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), iniFile);
            
            // Now load all configurations in this priority list, include the command line again, to enable
            // overriding configuration settings via command line
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddIniFile(iniConfig, optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(Program.Args);

            Configuration = builder.Build();

            try
            {
                Configuration["IniFilePresent"] = File.Exists(iniConfig).ToString();
            }
            catch (Exception)
            {
                Configuration["IniFilePresent"] = false.ToString();
            }
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The service collection used to add new services.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc(options =>
            {
                options.OutputFormatters.Add(new XmlDataContractSerializerOutputFormatter());
            });

            // Register singleton classes with DI container
            services.AddSingleton<IHostService, HostService>();
            services.AddSingleton<IBackgroundJob, BackgroundJob>();
            services.AddSingleton<IConfiguration>(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory
                .AddConsole(Configuration.GetSection("Logging"))
                .AddDebug();

            var logPath = Path.Combine(Path.DirectorySeparatorChar.ToString(), "var", "log", "sosync", Configuration["instance"]);

            // In development mode on windows, save the log file within the app directory
            if (env.IsDevelopment() && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            }

            var log = (Microsoft.Extensions.Logging.ILogger)loggerFactory.CreateLogger<Startup>();

            try
            {
                if (!Directory.Exists(logPath))
                    Directory.CreateDirectory(logPath);
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore permission errors when checking log directoy
            }

            var logFile = Path.Combine(logPath, $"{Configuration["instance"]}.log");

            try
            {
                // If log file is not found, try to create a new one in order to provoke
                // an exception
                if (!File.Exists(logFile))
                    File.Create(logFile);

                LogEventLevel lvl;
                Enum.TryParse<LogEventLevel>(Configuration["Logging:LogLevel:Default"], out lvl);

                LoggerConfiguration logConfig = new LoggerConfiguration()
                    .MinimumLevel.Is(lvl)
                    .WriteTo.File(path: logFile, outputTemplate: "{Timestamp:o} [{Level}] {Message}{NewLine}{Exception}");

                loggerFactory.AddSerilog(logConfig.CreateLogger());

                log.LogInformation($"Logging to: {logFile}");
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore permission errors, but log to console
                log.LogWarning($"Cannot log to \"{logFile}\": Access denied.");
            }
            catch (IOException ex)
            {
                // Ignore IO errors, but log to console
                log.LogWarning(ex.Message);
            }

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseMvc();
        }
        #endregion

        #region Properties
        public IConfigurationRoot Configuration { get; }
        #endregion
    }
}
