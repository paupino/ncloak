namespace TiviT.NCloak.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            //Basic parsing of arguments for now
            var parser = new CommandLineArgumentParser();
            parser.Parse(args);

            //Create a cloak manager
            var manager = new CloakManager();
            //Create a cloaking context
            var cloakContext = new CloakContext(parser.Settings);
            //Run the manager
            manager.Run(cloakContext);
        }
    }
}
