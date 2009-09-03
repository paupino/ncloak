using System;
using System.IO;
using System.Reflection;

namespace TiviT.NCloak.Console
{
    public class CommandLineArgumentParser
    {
        private readonly InitialisationSettings settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineArgumentParser"/> class.
        /// </summary>
        public CommandLineArgumentParser()
        {
            settings = new InitialisationSettings();
        }

        /// <summary>
        /// Gets the settings.
        /// </summary>
        /// <value>The settings.</value>
        public InitialisationSettings Settings
        {
            get { return settings; }
        }

        /// <summary>
        /// Parses the specified command line arguments.
        /// </summary>
        /// <param name="args">The args.</param>
        public void Parse(string[] args)
        {
            //Simple parsing for now
            foreach (string arg in args)
                settings.AssembliesToObfuscate.Add(arg);
            
            //Set the output directory to the current directory + Obfuscated
            string currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (String.IsNullOrEmpty(currentDir))
            {
                System.Console.Error.WriteLine("Unable to determine current directory...");
                Environment.Exit(1);
                return;
            }

            //Build the output directory
            string outputDir = Path.Combine(currentDir, "Obfuscated");
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            //Set it
            Settings.OutputDirectory = outputDir;
        }
    }
}
