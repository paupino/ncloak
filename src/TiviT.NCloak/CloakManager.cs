using System;
using System.Collections.Generic;
using TiviT.NCloak.CloakTasks;

namespace TiviT.NCloak
{
    public class CloakManager
    {
        private readonly List<ICloakTask> cloakingTasks;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloakManager"/> class.
        /// </summary>
        public CloakManager()
        {
            cloakingTasks = new List<ICloakTask>();
        }

        /// <summary>
        /// Registers the cloaking task in the job pipeline.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void RegisterTask<T>() where T : ICloakTask
        {
            ICloakTask task = Activator.CreateInstance<T>();
            cloakingTasks.Add(task);
        }

        /// <summary>
        /// Registers the cloaking task in the job pipeline.
        /// </summary>
        /// <param name="task">The task.</param>
        public void RegisterTask(ICloakTask task)
        {
            if (task == null) throw new ArgumentNullException("task");
            cloakingTasks.Add(task);
        }

        /// <summary>
        /// Configures the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        public void Configure(ICloakContext context)
        {
            if (context == null) throw new ArgumentNullException("context");

            //Simplify the task
            RegisterTask<SimplifyTask>();

            //Encrypt strings before anything else
            if (context.Settings.EncryptStrings) 
                RegisterTask(new StringEncryptionTask(StringEncryptionMethod.Xor));

            //Build up a mapping of the assembly and obfuscate
            if (!context.Settings.NoRename)
            {
                RegisterTask<MappingTask>();
                RegisterTask<ObfuscationTask>();
            }

            //Supress ILDASM decompilation
            if (context.Settings.SupressIldasm)
                RegisterTask<SupressIldasmTask>();

            //Try to confuse reflection
            if (context.Settings.ConfuseDecompilationMethod != ConfusionMethod.None)
                RegisterTask(new ConfuseDecompilationTask(ConfusionMethod.InvalidIl));

            //Optimize the assembly (turn into short codes where poss)
            if (context.Settings.ConfuseDecompilationMethod == ConfusionMethod.None) //HACK: The new Mono.Cecil doesn't like bad IL codes
                RegisterTask<OptimizeTask>();

            //Always last - output the assembly in the relevant format
            if (String.IsNullOrEmpty(context.Settings.TamperProofAssemblyName))
                RegisterTask<OutputAssembliesTask>(); //Default
            else
                RegisterTask<TamperProofTask>(); //Tamper proofing combines all assemblies into one
        }


        /// <summary>
        /// Runs the clock process.
        /// </summary>
        public void Run(ICloakContext context)
        {
            //Back stop - allows for tests to include only the relevant tasks
            if (cloakingTasks.Count == 0)
                Configure(context);

            //Make sure we have a context
            if (context == null) throw new ArgumentNullException("context");

            //Run through each of our tasks
            foreach (ICloakTask task in cloakingTasks)
            {
                OutputHelper.WriteTask(task);
                task.RunTask(context);
            }
        }
    }
}
