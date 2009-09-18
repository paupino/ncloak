using System;
using System.IO;

namespace TiviT.NCloak.Tests
{
    public static class ObfuscationHelper
    {
        public static void Obfuscate(params string[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
                throw new ArgumentException("No assmemblies provided to obfuscate", "assemblies");

            //Run the obfuscator
            CloakManager manager = new CloakManager();
            //Configure it (as necessary) - tasks etc
            manager.RegisterTask<CloakTasks.MappingTask>();
            manager.RegisterTask<CloakTasks.ObfuscationTask>();
            //Create a cloaking context
            InitialisationSettings settings = new InitialisationSettings();
            settings.AssembliesToObfuscate.AddRange(assemblies);
            settings.ObfuscateAllModifiers = true;
            settings.OutputDirectory = Path.Combine(Environment.CurrentDirectory,
                                                    "Obfuscated");
            if (!Directory.Exists(settings.OutputDirectory))
                Directory.CreateDirectory(settings.OutputDirectory);
            settings.Validate();
            ICloakContext cloakContext = new CloakContext(settings);
            //Run the manager
            manager.Run(cloakContext);
        }
    }
}
