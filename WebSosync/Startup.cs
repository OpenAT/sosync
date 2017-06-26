using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using WebSosync.Data.Models;
using WebSosync.Extensions;
using WebSosync.Helpers;
using WebSosync.Interfaces;

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
            // Minimal configuration: a command line parameter
            // specifying the INI
            var minConfig = new ConfigurationBuilder()
                .AddCommandLine(Program.Args, Program.SwitchMappings)
                .Build();
            
            // Now load all configurations in this priority list, include the command line again, to enable
            // overriding configuration settings via command line
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddIniFile(Program.ConfigurationINI, optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(Program.Args, Program.SwitchMappings);

            Configuration = builder.Build();

            // Map the logging configuration to all use sosync:log_level
            Configuration["Logging:LogLevel:Default"] = Configuration["sosync:log_level"];
            Configuration["Logging:LogLevel:System"] = Configuration["sosync:log_level"];
            Configuration["Logging:LogLevel:Microsoft"] = Configuration["sosync:log_level"];
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The service collection used to add new services.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.ConfigurePoco<SosyncOptions>(Configuration.GetSection("sosync"));

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
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IServiceProvider svc)
        {
            var sosyncConfig = svc.GetService<SosyncOptions>();

            loggerFactory
                .AddConsole(Configuration.GetSection("Logging"))
                .AddDebug();

            var log = (Microsoft.Extensions.Logging.ILogger)loggerFactory.CreateLogger<Startup>();
            var logFile = Path.GetFullPath(sosyncConfig.Log_File);

            try
            {
                // If log file is not found, try to create a new one in order to provoke
                // an exception right away (so logging errors are logged right at startup)

                if (!File.Exists(logFile))
                    File.Create(logFile).Dispose();

                LogLevel configLogLvl;
                Enum.TryParse<LogLevel>(sosyncConfig.Log_Level, out configLogLvl);
                LogEventLevel lvl = LogHelper.ConvertLevel(configLogLvl);

                LoggerConfiguration logConfig = new LoggerConfiguration()
                    .MinimumLevel.Is(lvl)
                    .WriteTo.File(
                        path: logFile,
                        outputTemplate: "{Timestamp:o} [{Level}] {Message}{NewLine}{Exception}"
                        );

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
