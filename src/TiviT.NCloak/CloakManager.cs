using System;

namespace TiviT.NCloak
{
    public class CloakManager
    {
        private readonly InitialisationSettings settings;

        public CloakManager(InitialisationSettings settings)
        {
            //Make sure it isn't null
            if (settings == null) throw new ArgumentNullException("settings");

            //Validate the settings
            settings.Validate();

            //Set the local version
            this.settings = settings;
        }

        /// <summary>
        /// Runs the clock process.
        /// </summary>
        public void Run()
        {
            //Later on we'll do this properly allowing them to pick and choose which tasks to run...
            ICloakTask task = new ObfuscationTask();
            task.RunTask(settings);
        }
    }
}
