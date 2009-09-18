using System.Collections.Generic;
using System;

namespace TiviT.NCloak.Mapping
{
    public class AssemblyMapping
    {
        private readonly string assemblyName;
        private readonly Dictionary<string, TypeMapping> typeMappingTable;
        private readonly Dictionary<string, string> obfuscatedToOriginalMapping;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyMapping"/> class.
        /// </summary>
        /// <param name="assemblyName">Name of the assembly.</param>
        public AssemblyMapping(string assemblyName)
        {
            this.assemblyName = assemblyName;
            typeMappingTable = new Dictionary<string, TypeMapping>();
            obfuscatedToOriginalMapping = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets the name of the assembly.
        /// </summary>
        /// <value>The name of the assembly.</value>
        public string AssemblyName
        {
            get { return assemblyName; }
        }

        /// <summary>
        /// Adds the type mapping to the assembly.
        /// </summary>
        /// <param name="typeName">Name of the type.</param>
        /// <param name="obfuscatedTypeName">Name of the obfuscated type.</param>
        /// <returns></returns>
        public TypeMapping AddType(string typeName, string obfuscatedTypeName)
        {
            TypeMapping typeMapping = new TypeMapping(typeName, obfuscatedTypeName);
            typeMappingTable.Add(typeName, typeMapping);
            //Add a reverse mapping
            if (!String.IsNullOrEmpty(obfuscatedTypeName))
                obfuscatedToOriginalMapping.Add(obfuscatedTypeName, typeName);
            return typeMapping;
        }

        /// <summary>
        /// Gets the type mapping.
        /// </summary>
        /// <param name="typeName">Name of the type.</param>
        /// <returns></returns>
        public TypeMapping GetTypeMapping(string typeName)
        {
            if (typeMappingTable.ContainsKey(typeName))
                return typeMappingTable[typeName];

            //Check the reverse mapping table
            if (obfuscatedToOriginalMapping.ContainsKey(typeName))
            {
                string originalTypeName = obfuscatedToOriginalMapping[typeName];
                if (typeMappingTable.ContainsKey(originalTypeName))
                    return typeMappingTable[originalTypeName];
            }
            return null;
        }
    }
}
