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

            //To demonstrate field access (pointless field access mind you) set the field "Name"
            service.Name = "MyService";

            //Register commands and start
            service.RegisterCommand<QuitCommand>();
            service.RegisterCommand<SayHelloCommand>();
            service.Start();

            //Display the "IsRunning" property - for no reason other than to prove property access is the same
            Console.Write("The CommandLineService {0} ", service.Name);
            if (service.IsRunning)
                Console.WriteLine("is still running");
            else
                Console.WriteLine("has stopped running");
        }
    }
}
