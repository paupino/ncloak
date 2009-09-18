namespace TiviT.NCloak.CloakTasks
{
    public interface ICloakTask
    {
        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        /// <param name="context">The running context of this cloak job.</param>
        void RunTask(ICloakContext context);
    }
}