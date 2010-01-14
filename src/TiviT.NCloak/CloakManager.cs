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
        /// Runs the clock process.
        /// </summary>
        public void Run(ICloakContext context)
        {
            //Make sure we have a context
            if (context == null) throw new ArgumentNullException("context");

            //Run through each of our tasks
            foreach (ICloakTask task in cloakingTasks)
            {
#if DEBUG
                Console.WriteLine("==== Executing task: {0} ====", task.GetType().FullName);
#endif
                task.RunTask(context);
            }
        }
    }
}
