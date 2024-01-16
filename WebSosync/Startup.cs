using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Syncer.Services;
using Syncer.Workers;
using System;
using System.IO;
using System.Linq;
using WebSosync.Common;
using WebSosync.Common.Interfaces;
using WebSosync.Data;
using WebSosync.Data.Helpers;
using WebSosync.Data.Models;
using WebSosync.Extensions;
using WebSosync.Helpers;
using WebSosync.Interfaces;
using WebSosync.Models;
using WebSosync.Services;

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
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
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
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

            // Register the SosyncOptions class as a singleton, configured with
            // the configuration section "sosync"
            services.ConfigurePoco<SosyncOptions>(Configuration.GetSection("sosync"));
            services.AddSingleton<IThreadSettings, ThreadSettings>();

            // Add framework services.
            services.AddMvc(options =>
            {
                // Add input and output formatters
                options.InputFormatters.Add(new XmlDataContractSerializerInputFormatter(null));
                options.OutputFormatters.Add(new XmlDataContractSerializerOutputFormatter());
                options.EnableEndpointRouting = false;

                var jsonOutputFormatter = options.OutputFormatters
                    .Where(f => f is SystemTextJsonOutputFormatter)
                    .Select(f => f as SystemTextJsonOutputFormatter)
                    .SingleOrDefault();

                if (jsonOutputFormatter != null)
                {
                    jsonOutputFormatter.SerializerOptions.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
                }
            });

            // Dependency Injection (DI) cheat list:
            // - AddSingleton 1 instance entire webserver/program
            // - AddScoped creates an instance per web-request
            // - AddTransient creates an instance per service call

            // What about disposing? DI container takes care of that.

            // Register singleton classes with DI container
            services.AddLogging(opt => opt.AddConsole());
            services.AddSingleton<IServiceCollection>(services);
            services.AddSingleton<IConfiguration>(Configuration);
            services.AddSingleton<TimeService>();
            services.AddSingleton<OdooService>();
            services.AddSingleton<SerializationService>();
            services.AddSingleton<GitService>();
            services.AddSingleton<OdooFormatService>();
            services.AddSingleton<StatisticService>();
            services.AddSingleton<TypeService>();
            services.AddSingleton<HtmlService>();
            services.AddSingleton<FsoDataServiceFactory>();

            // Add background services
            services.AddHostedService<TokenBatchService>();

            var mailService = new MailService("smtpgateway.datadialog.net", 0, "contact@datadialog.net");
            services.AddSingleton<IMailService>(mailService);

            var flowService = new FlowService();
            services.AddSingleton(flowService);

            // Transient services
            services.AddTransient<DataService>();
            services.AddTransient<FsoDataService>();
            services.AddTransient<FlowCheckService>();
            services.AddTransient<MdbService>();
            services.AddTransient<SyncServiceCollection>();

            // Db contexts
            services.AddDbContext<SosyncGuiContext>(options =>
            {
                var conf = Configuration.GetSection("sosync");
                var host = conf.GetValue<string>("db_host");
                var port = conf.GetValue<int>("db_port");
                var dbname = conf.GetValue<string>("db_name");
                var user = conf.GetValue<string>("db_user");
                var pass = conf.GetValue<string>("db_user_pw");

                var conStr = ConnectionHelper.GetPostgresConnectionString(host, port, dbname, user, pass);
                options.UseNpgsql(conStr);
            });

            // Automatic registering of all data flows in the syncer project
            flowService.RegisterFlows(services);

            RegisterBackgroundJob<SyncWorker>(services);
        }

        /// <summary>
        /// Registers a background for a class, and the class itself, for use with dependency injection.
        /// </summary>
        /// <typeparam name="T">The type that will be used as background job.</typeparam>
        /// <param name="services">The dependency injection container.</param>
        private void RegisterBackgroundJob<T>(IServiceCollection services) where T : class, IBackgroundJobWorker
        {
            services.AddTransient<T>();
            services.AddSingleton<IBackgroundJob<T>, BackgroundJob<T>>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IServiceProvider svc)
        {
            var sosyncConfig = svc.GetService<SosyncOptions>();
            var log = (Microsoft.Extensions.Logging.ILogger)loggerFactory.CreateLogger<Startup>();
            var logFile = Path.GetFullPath(sosyncConfig.Log_File);

            try
            {
                // If log file is not found, try to create a new one in order to provoke
                // an exception right away (so logging errors are logged right at startup)

                if (!File.Exists(logFile))
                    File.Create(logFile).Dispose();

                Enum.TryParse<LogLevel>(sosyncConfig.Log_Level, out LogLevel configLogLvl);
                LogEventLevel lvl = LogHelper.ConvertLevel(configLogLvl);

                LoggerConfiguration logConfig = new LoggerConfiguration()
                    .MinimumLevel.Is(lvl)
                    .Enrich.WithEnvironmentUserName()
                    .WriteTo.File(
                        path: logFile,
                        outputTemplate: "{Timestamp:o} " + SystemHelper.WhoAmI() + " [{Level}] {Message}{NewLine}{Exception}"
                        );

                loggerFactory.AddSerilog(logConfig.CreateLogger());

                Program.LogFile = logFile;
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
