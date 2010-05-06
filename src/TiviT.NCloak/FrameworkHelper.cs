using System.Collections.Generic;
using System.Runtime.InteropServices;
using Mono.Cecil;

namespace TiviT.NCloak
{
    internal static class FrameworkHelper
    {
        private static readonly Dictionary<string, AssemblyDefinition> cache = new Dictionary<string,AssemblyDefinition>();

        public static TypeDefinition Find(string assemblyName, string fullTypeName)
        {
            //Get the assembly
            AssemblyDefinition ad;
            if (cache.ContainsKey(assemblyName))
                ad = cache[assemblyName];
            else
            {
                ad = AssemblyDefinition.ReadAssembly(RuntimeEnvironment.GetRuntimeDirectory() + "\\" + assemblyName);
                cache.Add(assemblyName, ad);
            }

            //Find the type
            foreach (TypeDefinition td in ad.MainModule.Types)
            {
                if (td.FullName == fullTypeName)
                    return td;
            }
            return null;
        }
    }
}
