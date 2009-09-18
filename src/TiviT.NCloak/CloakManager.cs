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
        /// Runs the clock process.
        /// </summary>
        public void Run(ICloakContext context)
        {
            //Make sure we have a context
            if (context == null) throw new ArgumentNullException("context");

            //Run through each of our tasks
            foreach (ICloakTask task in cloakingTasks)
                task.RunTask(context);
        }
    }
}
