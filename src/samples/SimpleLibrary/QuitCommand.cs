namespace SimpleLibrary
{
    public class QuitCommand : CommandBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QuitCommand"/> class.
        /// </summary>
        /// <param name="commandLineService">The command line service.</param>
        public QuitCommand(CommandLineService commandLineService) 
            : base(commandLineService, "quit")
        {
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns></returns>
        protected override int ExecuteCommand(string[] args)
        {
            //Tell the command line service to stop running
            CommandLineService.IsRunning = false;
            return 0;
        }
    }
}
