using Mono.Cecil;
using Mono.Cecil.Cil;
using System;

namespace TiviT.NCloak.CloakTasks
{
    /// <summary>
    /// Optimizes the assemblies (i.e. turns long codes to short codes where necessary)
    /// </summary>
    public class OptimizeTask : ICloakTask
    {
        /// <summary>
        /// Gets the task name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "Optimizing methods"; }
        }

        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        /// <param name="context">The running context of this cloak job.</param>
        public void RunTask(ICloakContext context)
        {
            //Optimization on it's own will screw up our original changes so let's fix it up first
            context.ReloadAssemblyDefinitions();

            //Now get the dictionary and optimize
            var dictionary = context.GetAssemblyDefinitions();
            foreach (AssemblyDefinition assembly in dictionary.Values)
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
                                OutputHelper.WriteMethod(typeDefinition, methodDefinition);
                                methodDefinition.Body.Optimize();
                            }
                        }
                    }
                }
            }
        }
    }
}
