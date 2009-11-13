using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using System;

namespace TiviT.NCloak.CloakTasks
{
    public class SupressIldasmTask : ICloakTask
    {
        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        /// <param name="context">The running context of this cloak job.</param>
        public void RunTask(ICloakContext context)
        {
            Dictionary<string, AssemblyDefinition> assemblyCache = context.GetAssemblyDefinitions();
            foreach (string assembly in assemblyCache.Keys)
            {
                AssemblyDefinition def = assemblyCache[assembly];
                Type si = typeof (SuppressIldasmAttribute);
                CustomAttribute found = null;
                foreach (CustomAttribute attr in def.CustomAttributes)
                {
                    if (attr.Constructor.DeclaringType.FullName == si.FullName)
                    {
                        found = attr;
                        break;
                    }
                }

                //Only add if it's not there already
                if (found == null)
                {
                    //Add one
                    MethodReference constructor = def.MainModule.Import(typeof (SuppressIldasmAttribute).GetConstructor(Type.EmptyTypes));
                    CustomAttribute attr = new CustomAttribute(constructor);
                    def.CustomAttributes.Add(attr);
                }
            }

        }
    }
}
