using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebSosync.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Binds the configuration to a plain old CLR object (POCO) and registers it with the
        /// dependency injection container.
        /// </summary>
        /// <typeparam name="T">The configuration type (the POCO).</typeparam>
        /// <param name="services">The dependency injection service.</param>
        /// <param name="configuration">The configuration to bind the POCO to.</param>
        /// <returns>A new instance of <see cref="T"/> containing the values from the <see cref="IConfiguration"/> instance.</returns>
        public static T ConfigurePoco<T>(this IServiceCollection services, IConfiguration configuration) where T : class, new()
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var config = new T();
            configuration.Bind(config);
            services.AddSingleton(config);

            return config;
        }
    }
}
