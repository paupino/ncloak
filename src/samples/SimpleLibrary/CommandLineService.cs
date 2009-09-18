using System;
using System.Collections.Generic;

namespace SimpleLibrary
{
    /// <summary>
    /// Very basic command line service to demonstrate generics,
    /// and protected funcitons
    /// </summary>
    public class CommandLineService
    {
        private readonly Dictionary<string, CommandBase> commands;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineService"/> class.
        /// </summary>
        public CommandLineService()
        {
            commands = new Dictionary<string, CommandBase>();
        }

        /// <summary>
        /// Registers the command.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void RegisterCommand<T>() where T : CommandBase
        {
            CommandBase command = (CommandBase)Activator.CreateInstance(typeof (T), this);
            commands.Add(command.CommandName, command);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is running.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is running; otherwise, <c>false</c>.
        /// </value>
        public bool IsRunning { get; set; }

        /// <summary>
        /// Determines whether the given command name is valid.
        /// </summary>
        /// <param name="commandName">The command name.</param>
        /// <returns>
        /// 	<c>true</c> if it is a valid command; otherwise, <c>false</c>.
        /// </returns>
        public bool IsValidCommand(string commandName)
        {
            if (String.IsNullOrEmpty(commandName))
                return false;
            return commands.ContainsKey(commandName.ToLower());
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        public void Start()
        {
            Console.WriteLine("Starting Command Line Service...");
            Console.WriteLine("This is a very basic demonstration class for multiple assembly and modifier\nobfuscation");
            Console.WriteLine("Registered commands are:");
            foreach (string c in commands.Keys)
                Console.Write(c + " ");
            Console.WriteLine();
            IsRunning = true;
            while (IsRunning)
            {
                //Prompt
                Console.Write("> ");
                string line = Console.ReadLine();
                if (String.IsNullOrEmpty(line) || line.Trim().Length == 0)
                    continue;
                line = line.Trim();

                //Parse out the command and arguments
                string command, args;
                int firstSpace;
                if ((firstSpace = line.IndexOf(' ')) > 0)
                {
                    command = line.Substring(0, firstSpace).ToLower();
                    if (firstSpace + 1 < line.Length)
                        args = line.Substring(firstSpace + 1).Trim();
                    else
                        args = null;
                }
                else
                {
                    command = line.Trim().ToLower();
                    args = null;
                }
                //Execute the command
                ExecuteCommand(command, args == null ? null : args.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="args">The args.</param>
        public void ExecuteCommand(string command, string[] args)
        {
            //Find the command otherwise error
            if (commands.ContainsKey(command))
                commands[command].Run(args);
            else
                Console.WriteLine("Command not found");
        }
    }
}
