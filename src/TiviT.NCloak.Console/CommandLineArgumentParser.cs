using System;
using System.IO;
using System.Reflection;
using TiviT.NCloak.CloakTasks;

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
            //If we have no arguments then get out of here
            if (args == null || args.Length == 0)
            {
                System.Console.Error.WriteLine("The syntax of this command is incorrect.");
                System.Console.Error.WriteLine("Use -? for help on how to use this program");
                Environment.Exit(1);
                return;
            }

            //Go through and parse the arguments
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-") || args[i].StartsWith("/"))
                {
                    string argKey = args[i].Substring(1);
                    string argValue = null;
                    if (argKey.Contains("="))
                    {
                        int index = argKey.IndexOf('=');
                        if (argKey.Length > index + 1)
                            argValue = argKey.Substring(index + 1);
                        argKey = argKey.Substring(0, index);
                    }
                    //Standard argument
                    switch (argKey)
                    {
                        case "out":
                            //Set the output directory
                            if (i + 1 < args.Length)
                                SetOutputDirectory(args[++i]);
                            else
                                DisplayError("Unrecognised number of arguments for out parameter");
                            break;
                        
                        case "full":
                            //Apply full obfuscation to public members
                            settings.ObfuscateAllModifiers = true;
                            break;

                        case "norename":
                            //Used for debugging purposes
                            settings.NoRename = true;
                            break;

                        case "strings":
                            settings.EncryptStrings = true;
                            break;

                        case "suppressildasm":
                            switch (argValue)
                            {
                                case "0":
                                    settings.SupressIldasm = false;
                                    break;
                                case "1":
                                    settings.SupressIldasm = true;
                                    break;
                                default:
                                    DisplayError("Unrecognized suppress ildasm switch");
                                    break;
                            }
                            
                            break;

                        case "confuse":
                            settings.ConfuseDecompilationMethod = ConfusionMethod.InvalidIl;
                            break;

                        case "tamperproof":
                            //Set the tamper proof name
                            if (i + 1 < args.Length)
                            {
                                try
                                {
                                    settings.TamperProofAssemblyName = args[++i];
                                }
                                catch (FormatException)
                                {
                                    DisplayError("The tamperproof parameter must be a valid .NET name");
                                }
                            }
                            else
                                DisplayError("Unrecognised number of arguments for tamperproof parameter");
                            break;

                        case "bootstrappertype":
                        case "bootstraptype":
                            if (String.IsNullOrEmpty(argValue))
                                DisplayError("Bootstrapper type must be either windows or console (default)");
                            else
                                switch (argValue.ToLower())
                                {
                                    case "windows":
                                        settings.TamperProofAssemblyType = AssemblyType.Windows;
                                        break;
                                    case "console":
                                        settings.TamperProofAssemblyType = AssemblyType.Console;
                                        break;
                                    default:
                                        DisplayError("Bootstrapper type must be either windows or console (default)");
                                        break;
                                }
                            break;

                        case "?":
                        case "help":
                            DisplayUsage();
                            Environment.Exit(1);
                            return;

                        default:
                            DisplayError("Unrecognised command line option");
                            break;
                    }
                }
                else
                {
                    //Likely to be an assembly to obfuscate
                    settings.AssembliesToObfuscate.Add(args[i]);
                }
            }
            
            //If we haven't set the output directory then do it automatically
            if (String.IsNullOrEmpty(Settings.OutputDirectory))
            {
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
                SetOutputDirectory(outputDir);
            }
        }

        /// <summary>
        /// Displays the usage.
        /// </summary>
        private static void DisplayUsage()
        {
            System.Console.WriteLine();
            System.Console.WriteLine("=====================================================");
            System.Console.WriteLine("NCloak - the open source .NET code protection utility");
            System.Console.WriteLine("Written by Paul Mason (c) 2009");
            System.Console.WriteLine("=====================================================");
            System.Console.WriteLine();
            System.Console.WriteLine("Released under the MIT license; for access to the latest version, source");
            System.Console.WriteLine("code, and bug reporting please visit http://code.google.com/p/ncloak/");
            System.Console.WriteLine();
            System.Console.WriteLine("Usage:");
            System.Console.WriteLine();
            System.Console.WriteLine(" {0} [options] assemblies", Assembly.GetExecutingAssembly().GetName().Name);
            System.Console.WriteLine();
            System.Console.WriteLine("  /full\t\t\tSpecifies that all members should be included in ");
            System.Console.WriteLine("  \t\t\tobfuscation");

            System.Console.WriteLine("  /out\t\t\tSpecifies the output location of all protected ");
            System.Console.WriteLine("  \t\t\tassemblies");
            
            System.Console.WriteLine("  /strings\t\tSpecifies that the obfuscator encrypts string constants");
            
            System.Console.WriteLine("  /confuse\t\tIf specified, the obfuscator attempts to stop ");
            System.Console.WriteLine("  \t\t\tdecompilation using various techniques");
            
            System.Console.WriteLine("  /suppressildasm={0|1}\tSpecifieds whether to suppress disassembly to IL");
            System.Console.WriteLine("  \t\t\tusing the ildasm.exe tool (default is on)");
            
            System.Console.WriteLine("  /norename\t\tTurns off member renaming");

            System.Console.WriteLine("  /strings\t\tEnables encryption of strings");

            System.Console.WriteLine("  /tamperproof name\tPackages the output assemblies into a tamperproofed");
            System.Console.WriteLine("  \t\t\tbootstrapper assembly");

            System.Console.WriteLine("  /bootstraptype type\tSpecifies the output type of the bootstrapper");
            System.Console.WriteLine("  \t\t\tassembly when tamperproofing. Options: console|windows");

            System.Console.WriteLine("  assemblies\t\tSpecifies the assemblies to include in the code ");
            System.Console.WriteLine("  \t\t\tprotection tasks");
            System.Console.WriteLine();
        }

        /// <summary>
        /// Outputs an initialisation error.
        /// </summary>
        /// <param name="message">The message.</param>
        private static void DisplayError(string message)
        {
            System.Console.WriteLine(message);
            Environment.Exit(1);
        }

        private void SetOutputDirectory(string outputDir)
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            //Set it
            Settings.OutputDirectory = outputDir;
        }
    }
}

