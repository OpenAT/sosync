﻿using Microsoft.AspNetCore.Builder;
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
using WebSosync.Helpers;
using WebSosync.Models;
using Microsoft.Extensions.Options;

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
                .AddCommandLine(Program.Args)
                .Build();
            
            // Now load all configurations in this priority list, include the command line again, to enable
            // overriding configuration settings via command line
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddIniFile(Program.ConfigurationINI, optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(Program.Args);

            Configuration = builder.Build();
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The service collection used to add new services.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.Configure<SosyncConfiguration>(Configuration);

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
            var sosyncConfig = svc.GetService<IOptions<SosyncConfiguration>>();

            loggerFactory
                .AddConsole(Configuration.GetSection("Logging"))
                .AddDebug();

            var log = (Microsoft.Extensions.Logging.ILogger)loggerFactory.CreateLogger<Startup>();
            var logFile = sosyncConfig.Value.Log_File;

            try
            {
                // If log file is not found, try to create a new one in order to provoke
                // an exception, right away, in order to log it direclty on startup

                if (!File.Exists(logFile))
                    File.Create(logFile).Dispose();

                LogLevel configLogLvl;
                Enum.TryParse<LogLevel>(Configuration["Logging:LogLevel:Default"], out configLogLvl);
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
