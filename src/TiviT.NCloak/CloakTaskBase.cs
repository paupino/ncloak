using System;

namespace TiviT.NCloak
{
    public abstract class CloakTaskBase : ICloakTask
    {
        private InitialisationSettings _settings;

        /// <summary>
        /// Gets the initialisation settings.
        /// </summary>
        /// <value>The settings.</value>
        protected InitialisationSettings Settings
        {
            get { return _settings; }
        }

        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public void RunTask(InitialisationSettings settings)
        {
            //Check for null
            if (settings == null) throw new ArgumentNullException("settings");

            //Validate the settings
            settings.Validate();

            //Set the local variable
            _settings = settings;

            //Run the task
            RunTask();
        }

        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        protected abstract void RunTask();
    }
}
