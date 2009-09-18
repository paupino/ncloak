using System;
using SimpleLibrary;

namespace SimpleProgram
{
    class Program
    {
        static void Main()
        {
            //Perform a simple greet
            IGreeter greeter = new EnglishGreeter();
            greeter.Greet(Environment.UserName);

            //Run the command line program - this demonstrates usage of protected fields
            var service = new CommandLineService();
            service.RegisterCommand<QuitCommand>();
            service.RegisterCommand<SayHelloCommand>();
            service.Start();
        }
    }
}
