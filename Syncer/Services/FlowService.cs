using Microsoft.Extensions.DependencyInjection;
using Syncer.Attributes;
using Syncer.Exceptions;
using Syncer.Flows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using WebSosync.Data.Constants;

namespace Syncer.Services
{
    public class FlowService
    {
        #region Members
        private bool _registered;
        #endregion

        #region Properties
        /// <summary>
        /// List of all types derived from <see cref="SyncFlow"/>.
        /// </summary>
        public ReadOnlyCollection<Type> FlowTypes { get; private set; }
        #endregion

        #region Constructors
        public FlowService()
        {
            FlowTypes = null;
            _registered = false;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Searches all flow type classes and registers them with
        /// dependency injection.
        /// </summary>
        public void RegisterFlows(IServiceCollection services)
        {
            var types = new List<Type>();
            types.AddRange(GetAllFlowTypes());
            FlowTypes = types.AsReadOnly();

            foreach (var flowType in FlowTypes)
                services.AddTransient(flowType);

            _registered = true;
        }

        private void CheckFlowRegistration()
        {
            if (!_registered)
                throw new MissingFlowRegistrationException($"Flow classes are not registered. Use {nameof(FlowService)}.{nameof(RegisterFlows)}() at program start.");
        }
        /// <summary>
        /// Checks a given type for the two required syncer attributes.
        /// </summary>
        /// <param name="flowType">The type that is checked to be a correct flow.</param>
        private void CheckFlowAttributes(Type flowType)
        {
            var attOnline = flowType.GetTypeInfo().GetCustomAttribute<OnlineModelAttribute>();
            var attStudio = flowType.GetTypeInfo().GetCustomAttribute<StudioModelAttribute>();

            if (attOnline == null)
                throw new MissingAttributeException(flowType.Name, nameof(OnlineModelAttribute));

            if (attStudio == null)
                throw new MissingAttributeException(flowType.Name, nameof(StudioModelAttribute));
        }

        /// <summary>
        /// Throws an exception if any sync flow is missing one of the two required attributes.
        /// </summary>
        public void ThrowOnMissingFlowAttributes()
        {
            foreach (var flowType in FlowTypes)
                CheckFlowAttributes(flowType);
        }

        /// <summary>
        /// Gets the type for the flow class by model name. Uses attributes
        /// to determine the flow. If no flow was found, a
        /// <see cref="NotSupportedException"/> is thrown.
        /// </summary>
        /// <param name="modelName">The model name to find a flow for. Can be FS modle name, or FSO model name.</param>
        /// <returns></returns>
        public Type GetFlow(string jobType, string modelName)
        {
            CheckFlowRegistration();

            modelName = modelName.ToLower();

            foreach (var type in FlowTypes)
            {
                var fsAtt = type.GetTypeInfo().GetCustomAttribute<StudioModelAttribute>();
                var fsoAtt = type.GetTypeInfo().GetCustomAttribute<OnlineModelAttribute>();

                if (string.IsNullOrEmpty(jobType))
                {
                    if (typeof(ReplicateSyncFlow).IsAssignableFrom(type))
                    {
                        if ((fsAtt != null && fsAtt.Name.ToLower() == modelName)
                            || (fsoAtt != null && fsoAtt.Name.ToLower() == modelName))
                            return type;
                    }
                }
                else if (jobType == SosyncJobSourceType.MergeInto)
                {
                    if (typeof(MergeSyncFlow).IsAssignableFrom(type))
                    {
                        if ((fsAtt != null && fsAtt.Name.ToLower() == modelName)
                            || (fsoAtt != null && fsoAtt.Name.ToLower() == modelName))
                            return type;
                    }
                }
                else if (jobType == SosyncJobSourceType.Delete)
                {
                    if (typeof(DeleteSyncFlow).IsAssignableFrom(type))
                    {
                        if ((fsAtt != null && fsAtt.Name.ToLower() == modelName)
                            || (fsoAtt != null && fsoAtt.Name.ToLower() == modelName))
                            return type;
                    }
                }
                else
                {
                    throw new NotSupportedException($"Could not find appropriate sync flow for job_source_type '{jobType}'.");
                }
            }

            throw new NotSupportedException($"No sync flow found for model '{modelName}' of job_source_type '{jobType}'");
        }

        /// <summary>
        /// Gets all types derived from <see cref="SyncFlow"/>.
        /// Only searches the assembly of the base class.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Type> GetAllFlowTypes()
        {
            return typeof(SyncFlow)
                .GetTypeInfo()
                .Assembly
                .GetTypes()
                .Where(x => typeof(SyncFlow) != x && x.IsAbstract == false && typeof(SyncFlow).IsAssignableFrom(x));
        }
        #endregion
    }
}
