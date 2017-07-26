using Microsoft.Extensions.DependencyInjection;
using Syncer.Attributes;
using Syncer.Exceptions;
using Syncer.Flows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace Syncer.Services
{
    public class FlowService
    {
        #region Members
        private IServiceCollection _services;
        private bool _registered;
        #endregion

        #region Properties
        /// <summary>
        /// List of all types derived from <see cref="SyncFlow"/>.
        /// </summary>
        public ReadOnlyCollection<Type> FlowTypes { get; private set; }
        #endregion

        #region Constructors
        public FlowService(IServiceCollection services)
        {
            FlowTypes = null;
            _services = services;
            _registered = false;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Gets an instance of the <see cref="FlowService"/> class to query all sync flow
        /// classes, and registers all sync flow classes with dependency injection.
        /// </summary>
        /// <param name="services">The service collection used to add new services.</param>
        public void RegisterFlows()
        {
            var types = new List<Type>();
            types.AddRange(GetAllFlowTypes());
            FlowTypes = types.AsReadOnly();

            var svc = _services.BuildServiceProvider();
            var flowManager = svc.GetService<FlowService>();

            foreach (var flowType in flowManager.FlowTypes)
            {
                CheckFlowAttributes(flowType);
                _services.AddTransient(flowType);
            }

            _registered = true;
        }

        private void CheckFlowRegistration()
        {
            if (!_registered)
                throw new MissingFlowRegistrationException($"Flow classes are not registered. Use {nameof(FlowService)}.{nameof(RegisterFlows)}() at program start.");
        }
        /// <summary>
        /// Checks a given type for the two required syncer attributes. If any attribute
        /// is missing, throw an exception.
        /// </summary>
        /// <param name="flowType"></param>
        private void CheckFlowAttributes(Type flowType)
        {
            CheckFlowRegistration();

            var attOnline = flowType.GetTypeInfo().GetCustomAttribute<OnlineModelAttribute>();
            var attStudio = flowType.GetTypeInfo().GetCustomAttribute<StudioModelAttribute>();

            if (attOnline == null)
                throw new MissingAttributeException(flowType.Name, nameof(OnlineModelAttribute));

            if (attStudio == null)
                throw new MissingAttributeException(flowType.Name, nameof(StudioModelAttribute));
        }

        /// <summary>
        /// Gets the type for the flow class by model name. Uses attributes
        /// to determine the flow. If no flow was found, a
        /// <see cref="NotSupportedException"/> is thrown.
        /// </summary>
        /// <param name="modelName">The model name to find a flow for. Can be FS modle name, or FSO model name.</param>
        /// <returns></returns>
        public Type GetFlow(string modelName)
        {
            CheckFlowRegistration();

            modelName = modelName.ToLower();

            foreach (var type in FlowTypes)
            {
                var fsAtt = type.GetTypeInfo().GetCustomAttribute<StudioModelAttribute>();
                var fsoAtt = type.GetTypeInfo().GetCustomAttribute<OnlineModelAttribute>();

                if (fsAtt != null && fsAtt.Name.ToLower() == modelName)
                    return type;
                else if (fsoAtt != null && fsoAtt.Name.ToLower() == modelName)
                    return type;
            }

            throw new NotSupportedException($"No sync flow found for model \"{modelName}\"");
        }

        /// <summary>
        /// Gets all types derived from <see cref="SyncFlow"/>.
        /// Only searches the assembly of the base class.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Type> GetAllFlowTypes()
        {
            CheckFlowRegistration();

            return typeof(SyncFlow)
                .GetTypeInfo()
                .Assembly
                .GetTypes()
                .Where(x => typeof(SyncFlow) != x && typeof(SyncFlow).IsAssignableFrom(x));
        }
        #endregion
    }
}
