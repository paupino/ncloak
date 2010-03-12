using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TiviT.NCloak.CloakTasks
{
    /// <summary>
    /// Simplifies the assemblies (i.e. turns short codes to long codes)
    /// </summary>
    public class SimplifyTask : ICloakTask
    {
        /// <summary>
        /// Gets the task name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "Simplifying methods"; }
        }

        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        /// <param name="context">The running context of this cloak job.</param>
        public void RunTask(ICloakContext context)
        {
            foreach (AssemblyDefinition assembly in context.GetAssemblyDefinitions().Values)
            {
                Simplify(assembly);
            }
            context.ReloadAssemblyDefinitions();
        }

        /// <summary>
        /// Fixes the invalid instructions.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        private static void Simplify(AssemblyDefinition assembly)
        {
            //We'll search methods only at this point
            foreach (ModuleDefinition moduleDefinition in assembly.Modules)
            {
                //Go through each type
                foreach (TypeDefinition typeDefinition in moduleDefinition.Types)
                {
                    //Go through each method
                    foreach (MethodDefinition methodDefinition in typeDefinition.Methods)
                    {
                        if (methodDefinition.HasBody)
                        {
                            //Do the method
                            OutputHelper.WriteMethod(typeDefinition, methodDefinition);
                            methodDefinition.Body.Simplify();
                        }
                    }
                }
            }
        }
    }
}
