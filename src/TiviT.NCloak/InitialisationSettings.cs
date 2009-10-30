using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace TiviT.NCloak
{
    public class InitialisationSettings
    {
        private readonly List<string> assembliesToObfuscate;
        private bool validated;

        /// <summary>
        /// Initializes a new instance of the <see cref="InitialisationSettings"/> class.
        /// </summary>
        public InitialisationSettings()
        {
            assembliesToObfuscate = new List<string>();
            validated = false;
        }

        
        /// <summary>
        /// Gets a list of the assemblies to obfuscate.
        /// </summary>
        /// <value>The assemblies to obfuscate.</value>
        public List<string> AssembliesToObfuscate
        {
            get
            {
                return assembliesToObfuscate;
            }
        }

        /// <summary>
        /// Gets or sets the output directory.
        /// </summary>
        /// <value>The output directory.</value>
        public string OutputDirectory { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the obfuscator should obfuscate all access modifiers as opposed
        /// to just private access modifiers
        /// </summary>
        /// <value>
        /// 	<c>true</c> to obfuscate all modifiers; otherwise, <c>false</c>.
        /// </value>
        public bool ObfuscateAllModifiers
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the obfuscator should encrypt strings.
        /// </summary>
        /// <value><c>true</c> to encrypt strings; otherwise, <c>false</c>.</value>
        public bool EncryptStrings
        {
            get; set;
        }

        /// <summary>
        /// Validates the initialisation settings.
        /// </summary>
        public void Validate()
        {
            //Only validate if it hasn't already
            if (validated)
                return;

            //Check the assemblies to load
            if (assembliesToObfuscate.Count == 0)
                throw new InitialisationException("Must specify at least one assembly to obfuscate");
            
            //Make sure each file exists and that it is a valid assembly
            foreach (string assembly in assembliesToObfuscate)
            {
                //Check it exists
                if (!File.Exists(assembly))
                    throw new InitialisationException(String.Format("The specified assembly \"{0}\" does not exist", assembly));

                //Check it's a valid assembly
                try
                {
                    AssemblyName.GetAssemblyName(assembly);
                }
                catch (Exception ex)
                {
                    throw new InitialisationException(String.Format("The specified assembly \"{0}\" is not valid.", assembly), ex);
                }
            }

            //Check the output directory
            if (String.IsNullOrEmpty(OutputDirectory))
                throw new InitialisationException("An output directory is required");
            if (!Directory.Exists(OutputDirectory))
                throw new InitialisationException(String.Format("The output directory {0} does not currently exist.", OutputDirectory));

            //Set it to validated
            validated = true;
        }
    }
}
