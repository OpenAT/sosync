using Syncer.Attributes;
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
        #region Properties
        /// <summary>
        /// List of all types derived from <see cref="SyncFlow"/>.
        /// </summary>
        public ReadOnlyCollection<Type> FlowTypes { get; private set; }
        #endregion

        #region Constructors
        public FlowService()
        {
            var types = new List<Type>();
            types.AddRange(GetAllFlowTypes());
            FlowTypes = types.AsReadOnly();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Gets the type for the flow class by model name. Uses attributes
        /// to determine the flow. If no flow was found, a
        /// <see cref="NotSupportedException"/> is thrown.
        /// </summary>
        /// <param name="modelName">The model name to find a flow for. Can be FS modle name, or FSO model name.</param>
        /// <returns></returns>
        public Type GetFlow(string modelName)
        {
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
            return typeof(SyncFlow)
                .GetTypeInfo()
                .Assembly
                .GetTypes()
                .Where(x => typeof(SyncFlow) != x && typeof(SyncFlow).IsAssignableFrom(x));
        }
        #endregion
    }
}
