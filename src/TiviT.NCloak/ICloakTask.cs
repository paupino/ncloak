namespace TiviT.NCloak
{
    public interface ICloakTask
    {
        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        /// <param name="settings">The settings.</param>
        void RunTask(InitialisationSettings settings);
    }
}
