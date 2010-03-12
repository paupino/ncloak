using System;
using Mono.Cecil;
using TiviT.NCloak.CloakTasks;

namespace TiviT.NCloak
{
    /// <summary>
    /// For the meantime, this all goes to console, however in the future we may want to redirect this
    /// to another TextWriter
    /// </summary>
    public static class OutputHelper
    {
        public static void WriteMethod(TypeDefinition type, MethodDefinition method)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("> {0}.{1}.{2}", type.Namespace, type.Name, method.Name);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void WriteLine(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public static void WriteTask(ICloakTask task)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            string name = task.Name;
            Console.WriteLine(name);
            Console.WriteLine(new string('=', name.Length));
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}
