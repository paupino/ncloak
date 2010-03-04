using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleLibrary
{
    public class TestCommand : CommandBase
    {
        public TestCommand(CommandLineService commandLineService) 
            : base(commandLineService, "test")
        {
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns></returns>
        protected override int ExecuteCommand(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Please define what you would like to test. Current options: switch");
                return 0;
            }

            switch (args[0])
            {
                case "switch":
                    TestSwitchStatement(); //Already testing it aren't we?
                    break;
                default:
                    Console.WriteLine("Unknown option: {0}", args[0]);
                    break;
            }
            return 0;
        }

        private static void TestSwitchStatement()
        {
            switch (DateTime.Now.DayOfWeek)
            {
                case DayOfWeek.Sunday:
                    Console.WriteLine("It's Sunday - day off isn't it?");
                    break;
                case DayOfWeek.Monday:
                    Console.WriteLine("Monday... nothing else can be said");
                    break;
                case DayOfWeek.Tuesday:
                    Console.WriteLine("Tuesday aint so bad... :)");
                    break;
                case DayOfWeek.Wednesday:
                    Console.WriteLine("The middle of the work week! Wednesday.");
                    break;
                case DayOfWeek.Thursday:
                    Console.WriteLine("Thursday. A great day of the week!");
                    break;
                case DayOfWeek.Friday:
                    Console.WriteLine("Friday! Weekend tomorrow!");
                    break;
                case DayOfWeek.Saturday:
                    Console.WriteLine("Saturday - how can you not love them.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
