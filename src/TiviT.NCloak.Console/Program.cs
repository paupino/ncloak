namespace TiviT.NCloak.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            //Basic parsing of arguments for now
            CommandLineArgumentParser parser = new CommandLineArgumentParser();
            parser.Parse(args);

            //Run the cloak manager
            CloakManager manager = new CloakManager(parser.Settings);
            manager.Run();
        }
    }
}
