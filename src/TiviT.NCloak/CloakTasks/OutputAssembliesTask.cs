using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;

namespace TiviT.NCloak.CloakTasks
{
    public class OutputAssembliesTask : ICloakTask
    {
        /// <summary>
        /// Gets the task name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "Outputting assemblies"; }
        }

        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        /// <param name="context">The running context of this cloak job.</param>
        public void RunTask(ICloakContext context)
        {
            Dictionary<string, AssemblyDefinition> assemblyCache = context.GetAssemblyDefinitions();
            foreach (string assembly in assemblyCache.Keys)
            {
                //Save the assembly
                string outputPath = Path.Combine(context.Settings.OutputDirectory, Path.GetFileName(assembly));
                OutputHelper.WriteLine("Outputting assembly to {0}", outputPath);
                AssemblyFactory.SaveAssembly(assemblyCache[assembly], outputPath);
            }
        }
    }
}
