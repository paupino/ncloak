using System;

namespace SimpleLibrary
{
    public class SayHelloCommand : CommandBase
    {
        public SayHelloCommand(CommandLineService commandLineService) 
            : base(commandLineService, "hello")
        {
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns></returns>
        protected override int ExecuteCommand(string[] args)
        {
            IGreeter greeter = new EnglishGreeter();
            if (args == null || args.Length == 0)
                Console.WriteLine(greeter.Greet(Environment.UserName));
            else if (args.Length == 1)
                Console.WriteLine(greeter.Greet(args[0]));
            else
            {
                Console.WriteLine("Invalid arguments");
                return 1;
            }
            return 0;
        }
    }
}
