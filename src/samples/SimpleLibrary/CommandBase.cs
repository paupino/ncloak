using System;

namespace SimpleLibrary
{
    public abstract class CommandBase
    {
        private readonly CommandLineService commandLineService;
        private readonly string commandName;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandBase"/> class.
        /// </summary>
        /// <param name="commandLineService">The command line service.</param>
        /// <param name="commandName">The command name.</param>
        protected CommandBase(CommandLineService commandLineService, string commandName)
        {
            if (commandLineService == null) throw new ArgumentNullException("commandLineService");
            if (commandName == null) throw new ArgumentNullException("commandName");
            this.commandLineService = commandLineService;
            this.commandName = commandName.ToLower();
        }

        /// <summary>
        /// Gets the command name.
        /// </summary>
        /// <value>The command name.</value>
        public string CommandName
        {
            get { return commandName; }
        }

        /// <summary>
        /// Gets the command line service.
        /// </summary>
        /// <value>The command line service.</value>
        protected CommandLineService CommandLineService
        {
            get { return commandLineService; }
        }

        /// <summary>
        /// Runs the specified command with the given args.
        /// </summary>
        /// <param name="args">The args.</param>
        public void Run(string[] args)
        {
            //This could do argument checking before running the command
            int result = ExecuteCommand(args);
            if (result == 0)
                Console.WriteLine("Command completed successfully");
            else
                Console.WriteLine("Command failed to complete");
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns></returns>
        protected abstract int ExecuteCommand(string[] args);
    }
}
