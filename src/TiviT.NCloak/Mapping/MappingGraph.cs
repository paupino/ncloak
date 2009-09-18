using System.Collections.Generic;
using Mono.Cecil;

namespace TiviT.NCloak.Mapping
{
    public class MappingGraph
    {
        private readonly Dictionary<string, AssemblyMapping> assemblyMappings;

        /// <summary>
        /// Initializes a new instance of the <see cref="MappingGraph"/> class.
        /// </summary>
        public MappingGraph()
        {
            assemblyMappings = new Dictionary<string, AssemblyMapping>();
        }

        /// <summary>
        /// Adds a new assembly to the mapping table.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns></returns>
        public AssemblyMapping AddAssembly(AssemblyDefinition assembly)
        {
            AssemblyMapping mapping = new AssemblyMapping(assembly.Name.FullName);
            assemblyMappings.Add(assembly.Name.FullName, mapping);
            return mapping;
        }

        /// <summary>
        /// Determines whether an assembly mapping defined for the specified definition.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <returns>
        /// 	<c>true</c> if an assembly mapping is defined; otherwise, <c>false</c>.
        /// </returns>
        public bool IsAssemblyMappingDefined(AssemblyDefinition definition)
        {
            return IsAssemblyMappingDefined(definition.Name.FullName);
        }

        /// <summary>
        /// Determines whether an assembly mapping defined for the specified definition.
        /// </summary>
        /// <param name="assemblyName">Name of the assembly.</param>
        /// <returns>
        /// 	<c>true</c> if an assembly mapping is defined; otherwise, <c>false</c>.
        /// </returns>
        public bool IsAssemblyMappingDefined(string assemblyName)
        {
            return assemblyMappings.ContainsKey(assemblyName);
        }

        /// <summary>
        /// Gets the assembly mapping for the given definition.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <returns></returns>
        public AssemblyMapping GetAssemblyMapping(AssemblyDefinition definition)
        {
            return GetAssemblyMapping(definition.Name.FullName);
        }

        /// <summary>
        /// Gets the assembly mapping for the given definition.
        /// </summary>
        /// <param name="assemblyName">Name of the assembly.</param>
        /// <returns></returns>
        public AssemblyMapping GetAssemblyMapping(string assemblyName)
        {
            return assemblyMappings[assemblyName];
        }
    }
}
