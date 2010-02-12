using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text;

namespace TiviT.NCloak.CloakTasks
{
    public class TamperProofTask : ICloakTask
    {
        private const int KeySize = 256;
        private const int InitVectorSize = 16;
        private const int PasswordIterations = 2;

        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        /// <param name="context">The running context of this cloak job.</param>
        public void RunTask(ICloakContext context)
        {
            //WARNING: This task may potentially break complex applications. Please test this
            //thorougly before deployment.
            //
            //This task must be run last (in place of OutputAssembliesTask) as it does the following
            //  1. Encrypts all obfuscated assemblies supplied to this program
            //  2. Calculates the hash of each of the obfuscated assemblies (as opposed to encrypted)
            //  3. Creates a bootstrapper assembly including each of the assemblies as resources
            //     - The bootstrapper essentially extracts each of it's contents into memory (n.b. attack point)
            //       It then checks the hash of the bytes and if successful, loads the bytes into the current AppDomain.
            //
            
            //So getting down to business... the first think we need to do is go through each assembly and 
            //encrypt using Symmetric encryption
            //Lets generate a password
            string passwordKey = Guid.NewGuid().ToString();
            Guid salt = Guid.NewGuid();
            Rfc2898DeriveBytes password = new Rfc2898DeriveBytes(passwordKey, salt.ToByteArray(), PasswordIterations);
            //Get the key bytes and initialisation vector
            byte[] keyBytes = password.GetBytes(KeySize/8);
            byte[] initVector = password.GetBytes(InitVectorSize);

            //Go through each assembly, calculate the hash and encrypt
            Dictionary<string, byte[]> encryptedAssemblies = new Dictionary<string, byte[]>();
            Dictionary<string, byte[]> hashedAssemblies = new Dictionary<string, byte[]>();
            Dictionary<string, AssemblyDefinition> assemblies = context.GetAssemblyDefinitions();
            foreach (string assembly in assemblies.Keys)
            {
                //Get the raw data of the assembly
                byte[] assemblyRawData;
                AssemblyFactory.SaveAssembly(assemblies[assembly], out assemblyRawData);

                //Calculate the hash
                hashedAssemblies.Add(assembly, HashData(assemblyRawData));

                //Now encrypt it
                encryptedAssemblies.Add(assembly, EncryptData(assemblyRawData, keyBytes, initVector));
#if DEBUG
                //Output
                File.WriteAllBytes(Path.Combine(context.Settings.OutputDirectory, Path.GetFileName(assembly)), encryptedAssemblies[assembly]);
                File.WriteAllBytes(Path.Combine(context.Settings.OutputDirectory, Path.GetFileName(assembly) + ".v0"), hashedAssemblies[assembly]);
#endif
            }
#if DEBUG
            File.WriteAllText(Path.Combine(context.Settings.OutputDirectory, "password"), passwordKey + Environment.NewLine + Convert.ToBase64String(salt.ToByteArray()) + Environment.NewLine + Convert.ToBase64String(keyBytes) + Environment.NewLine + Convert.ToBase64String(initVector));
#endif

            //Now we've got that information - it's up to us to generate a bootstrapper assembly
            //We'll do this by starting from scratch
            AssemblyDefinition bootstrapperAssembly =
                AssemblyFactory.DefineAssembly(context.Settings.TamperProofAssemblyName, TargetRuntime.NET_2_0,
                context.Settings.TamperProofAssemblyType == AssemblyType.Windows ? AssemblyKind.Windows : AssemblyKind.Console);

            //Add some resources - encrypted assemblies
            foreach (string assembly in encryptedAssemblies.Keys)
            {
                //We'll randomise the names using the type table
                string resourceName = context.NameManager.GenerateName(NamingTable.Type);
                string hashName = resourceName + ".v0";
                bootstrapperAssembly.MainModule.Resources.Add(new EmbeddedResource(resourceName,
                                                                                   ManifestResourceAttributes.Private,
                                                                                   encryptedAssemblies[assembly]));
                bootstrapperAssembly.MainModule.Resources.Add(new EmbeddedResource(
                                                                  hashName,
                                                                  ManifestResourceAttributes.Private,
                                                                  hashedAssemblies[assembly]));

                //If it has an entry point then save this as well
                AssemblyDefinition def = assemblies[assembly];
                if (def.EntryPoint != null)
                {
                    StringBuilder entryPointHelper = new StringBuilder();
#if DEBUG
                    entryPointHelper.AppendLine(Path.GetFileName(assembly));
#else
                    entryPointHelper.AppendLine(resourceName);
#endif
                    entryPointHelper.AppendLine(def.EntryPoint.DeclaringType.Namespace + "." + def.EntryPoint.DeclaringType.Name);
                    entryPointHelper.AppendLine(def.EntryPoint.Name);
                    bootstrapperAssembly.MainModule.Resources.Add(new EmbeddedResource(
                                                                  resourceName + ".e",
                                                                  ManifestResourceAttributes.Private,
                                                                  Encoding.Unicode.GetBytes(entryPointHelper.ToString())));
#if DEBUG
                    File.WriteAllBytes(Path.Combine(context.Settings.OutputDirectory, "entry.e"), Encoding.Unicode.GetBytes(entryPointHelper.ToString()));
#endif
                }
            }

            //Now create a type to bootstrap this all
            TypeDefinition typeDef = new TypeDefinition(context.NameManager.GenerateName(NamingTable.Type),
                                                        context.NameManager.GenerateName(NamingTable.Type),
                                                        TypeAttributes.Public | TypeAttributes.Sealed, null);
            bootstrapperAssembly.MainModule.Types.Add(typeDef);
            //
            
            //Create an entry point
            MethodDefinition entryPoint = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                                               MethodAttributes.Static | MethodAttributes.Public,null);
            typeDef.Methods.Add(entryPoint);
            bootstrapperAssembly.EntryPoint = entryPoint;

            //Now make it do something
            CilWorker worker = entryPoint.Body.CilWorker;
            Instruction temp = worker.Create(OpCodes.Nop);
            worker.Append(temp);
            
            //Finally save this assembly to our output path
            string outputPath = Path.Combine(context.Settings.OutputDirectory, context.Settings.TamperProofAssemblyName + ".exe");
            Console.WriteLine("Outputting assembly to {0}", outputPath);
            AssemblyFactory.SaveAssembly(bootstrapperAssembly, outputPath);
        }

        /// <summary>
        /// Hashes the specified data using the SHA256 algorithm.
        /// </summary>
        /// <param name="rawData">The raw data.</param>
        /// <returns>A byte array pertaining to the hash of the raw data</returns>
        private static byte[] HashData(byte[] rawData)
        {
            SHA256 sha = SHA256.Create();
            return sha.ComputeHash(rawData);
        }

        /// <summary>
        /// Encrypts the specified data using Rijndael symmetric encryption.
        /// </summary>
        /// <param name="rawData">The raw data.</param>
        /// <param name="keyBytes">The key bytes.</param>
        /// <param name="initVector">The init vector.</param>
        /// <returns>A byte array of the encrypted data</returns>
        private static byte[] EncryptData(byte[] rawData, byte[] keyBytes, byte[] initVector)
        {
            //Use Rijndael encryption 
            Rijndael symmetricAlgorithm = Rijndael.Create();
            symmetricAlgorithm.Mode = CipherMode.CBC;

            //Generate an encryptor from the existing key bytes
            using (ICryptoTransform encryptor = symmetricAlgorithm.CreateEncryptor(keyBytes, initVector))
            {
                //Do the encryption
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        //Write the data into the crypto stream
                        cryptoStream.Write(rawData, 0, rawData.Length);

                        //Make sure that the final block is flushed
                        cryptoStream.FlushFinalBlock();

                        //Return the cipher bytes
                        return memoryStream.ToArray();
                    }
                }
            }
        }
    }
}
