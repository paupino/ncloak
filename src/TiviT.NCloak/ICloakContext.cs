using System.Collections.Generic;
using TiviT.NCloak.Mapping;
using Mono.Cecil;
namespace TiviT.NCloak
{
    public interface ICloakContext
    {
        /// <summary>
        /// Gets the settings.
        /// </summary>
        /// <value>The settings.</value>
        InitialisationSettings Settings { get; }

        /// <summary>
        /// Gets the name manager used to keep track of unique names for each type.
        /// </summary>
        /// <value>The name manager.</value>
        NameManager NameManager { get; }

        /// <summary>
        /// Gets the mapping graph.
        /// </summary>
        /// <value>The mapping graph.</value>
        MappingGraph MappingGraph { get; }

        /// <summary>
        /// Gets the assembly definitions to be processed; this caches
        /// the assembly definitions therefore needs to be treated as such.
        /// TODO: Change Dictionary to a readonly alternative
        /// </summary>
        /// <returns></returns>
        Dictionary<string, AssemblyDefinition> GetAssemblyDefinitions();
    }
}
