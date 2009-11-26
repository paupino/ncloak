using Mono.Cecil;

namespace TiviT.NCloak.CloakTasks
{
    public class ConfuseReflectorTask : ICloakTask
    {
        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        /// <param name="context">The running context of this cloak job.</param>
        public void RunTask(ICloakContext context)
        {
            //http://stackoverflow.com/questions/577403/how-do-commercial-obfuscators-achieve-to-crash-net-reflector-and-ildasm
        }
    }
}
