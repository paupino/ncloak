#if DEBUG
//#define USE_FRIENDLY_NAMING
//#define VERBOSE_OUTPUT
#define OUTPUT_PRE_TAMPER
#endif
#define USE_APPDOMAIN
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text;
using FieldAttributes=Mono.Cecil.FieldAttributes;
using MethodAttributes=Mono.Cecil.MethodAttributes;
using TypeAttributes=Mono.Cecil.TypeAttributes;
using System.Threading;

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
            byte[] keyBytes = password.GetBytes(KeySize / 8);
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
#if OUTPUT_PRE_TAMPER
                File.WriteAllBytes(context.Settings.OutputDirectory + "\\" + Path.GetFileName(assembly), assemblyRawData);
#endif

                //Calculate the hash
                hashedAssemblies.Add(assembly, HashData(assemblyRawData));

                //Now encrypt it
                encryptedAssemblies.Add(assembly, EncryptData(assemblyRawData, keyBytes, initVector));
            }

            //Now we've got that information - it's up to us to generate a bootstrapper assembly
            //We'll do this by starting from scratch
            AssemblyDefinition bootstrapperAssembly =
                AssemblyFactory.DefineAssembly(context.Settings.TamperProofAssemblyName, TargetRuntime.NET_2_0,
                context.Settings.TamperProofAssemblyType == AssemblyType.Windows ? AssemblyKind.Windows : AssemblyKind.Console);

            //Add some resources - encrypted assemblies
#if USE_FRIENDLY_NAMING
            const string resourceNamespace = "Resources";
#else
            string resourceNamespace = context.NameManager.GenerateName(NamingTable.Type);
#endif
            foreach (string assembly in encryptedAssemblies.Keys)
            {
                //We'll randomise the names using the type table
#if USE_FRIENDLY_NAMING
                string resourceName = resourceNamespace + "." + Path.GetFileName(assembly);
#else
                string resourceName = resourceNamespace + "." + context.NameManager.GenerateName(NamingTable.Type);
#endif
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
                    entryPointHelper.AppendLine(resourceName);
                    entryPointHelper.AppendLine(def.EntryPoint.DeclaringType.Namespace + "." + def.EntryPoint.DeclaringType.Name);
                    entryPointHelper.AppendLine(def.EntryPoint.Name);
                    bootstrapperAssembly.MainModule.Resources.Add(new EmbeddedResource(
                                                                  resourceName + ".e",
                                                                  ManifestResourceAttributes.Private,
                                                                  Encoding.Unicode.GetBytes(entryPointHelper.ToString())));
                }
            }

            //Now make it do something
            BuildBootstrapper(context, bootstrapperAssembly, passwordKey, salt);

            //Finally save this assembly to our output path
            string outputPath = Path.Combine(context.Settings.OutputDirectory, context.Settings.TamperProofAssemblyName + ".exe");
            Console.WriteLine("Outputting assembly to {0}", outputPath);
            AssemblyFactory.SaveAssembly(bootstrapperAssembly, outputPath);
        }

        /// <summary>
        /// Builds the main entry point to the bootstrapper.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The assembly.</param>
        /// <param name="passwordKey">The password key.</param>
        /// <param name="salt">The salt.</param>
        private static void BuildBootstrapper(ICloakContext context, AssemblyDefinition assembly, string passwordKey, Guid salt)
        {
            //See http://blog.paul-mason.co.nz/2010/02/tamper-proofing-implementation-part-2.html

            //First create the actual program runner
#if USE_FRIENDLY_NAMING
            TypeDefinition programType = new TypeDefinition("ProgramRunner",
                                                        "NCloak.Bootstrapper.Internal",
                                                        TypeAttributes.NotPublic | TypeAttributes.Class |
                                                        TypeAttributes.AutoClass | TypeAttributes.AnsiClass |
                                                        TypeAttributes.Serializable | TypeAttributes.BeforeFieldInit,
                                                        assembly.Import(typeof(object)));

#else
            TypeDefinition programType = new TypeDefinition(context.NameManager.GenerateName(NamingTable.Type),
                                                        context.NameManager.GenerateName(NamingTable.Type),
                                                        TypeAttributes.NotPublic | TypeAttributes.Class |
                                                        TypeAttributes.AutoClass | TypeAttributes.AnsiClass |
                                                        TypeAttributes.Serializable | TypeAttributes.BeforeFieldInit,
                                                        assembly.Import(typeof(object)));
#endif
            assembly.MainModule.Types.Add(programType);

            //Add the class level fields
#if USE_FRIENDLY_NAMING
            string assembliesLoadedVariableName = "assembliesLoaded";
            string assemblyLockVariableName = "assemblyLock";
            string executingAssemblyVariableName = "executingAssembly";
            string loadedAssembliesVariableName = "loadedAssemblies";
#else
            string assembliesLoadedVariableName = context.NameManager.GenerateName(NamingTable.Field);
            string assemblyLockVariableName = context.NameManager.GenerateName(NamingTable.Field);
            string executingAssemblyVariableName = context.NameManager.GenerateName(NamingTable.Field);
            string loadedAssembliesVariableName = context.NameManager.GenerateName(NamingTable.Field);
#endif
            var assembliesLoaded = new FieldDefinition(assembliesLoadedVariableName, assembly.Import(typeof(bool)), FieldAttributes.Private);
            var assemblyLock = new FieldDefinition(assemblyLockVariableName, assembly.Import(typeof(object)), FieldAttributes.Private | FieldAttributes.InitOnly);
            var executingAssembly = new FieldDefinition(executingAssemblyVariableName, assembly.Import(typeof(Assembly)),
                                                        FieldAttributes.Private | FieldAttributes.InitOnly);
            var loadedAssemblies = new FieldDefinition(loadedAssembliesVariableName,
                                                       assembly.Import(typeof(Dictionary<string, Assembly>)),
                                                       FieldAttributes.Private | FieldAttributes.InitOnly);
            programType.Fields.Add(assembliesLoaded);
            programType.Fields.Add(assemblyLock);
            programType.Fields.Add(executingAssembly);
            programType.Fields.Add(loadedAssemblies);

            //Get some method references we share
            MethodReference currentDomain = assembly.Import(typeof(AppDomain).GetProperty("CurrentDomain").GetGetMethod());
            MethodReference eventHandler = assembly.Import(typeof(ResolveEventHandler).GetConstructor(new[] { typeof(object), typeof(IntPtr) }));
            MethodReference assemblyResolve = assembly.Import(typeof(AppDomain).GetEvent("AssemblyResolve").GetAddMethod());
            MethodReference getExecutingAssembly = assembly.Import(typeof(Assembly).GetMethod("GetExecutingAssembly"));

            //Define decrypt data method
            var decryptMethod = BuildDecryptMethod(context, assembly);
            programType.Methods.Add(decryptMethod);

            //Define hash data method
            var hashMethod = BuildHashMethod(context, assembly);
            programType.Methods.Add(hashMethod);

            //Define load assembly
            var loadAssemblyMethod = BuildLoadAssemblyMethod(context, assembly, loadedAssemblies, executingAssembly, decryptMethod, hashMethod, passwordKey, salt);
            programType.Methods.Add(loadAssemblyMethod);

            //Define load type method
            var loadTypeMethod = BuildLoadTypeMethod(context, assembly, loadAssemblyMethod);
            programType.Methods.Add(loadTypeMethod);

            //Define load assemblies method
            var loadAssembliesMethod = BuildLoadAssembliesMethod(context, assembly, executingAssembly, loadAssemblyMethod);
            programType.Methods.Add(loadAssembliesMethod);

            //Define resolve method
            var resolveMethod = BuildResolveMethod(context, assembly, assemblyLock, assembliesLoaded, loadAssembliesMethod);
            programType.Methods.Add(resolveMethod);

            //Define start method
            var startMethod = BuildStartMethod(context, assembly, executingAssembly, assembliesLoaded, loadTypeMethod, loadAssembliesMethod);
            programType.Methods.Add(startMethod);

            //Now define a constructor
            BuildProgramConstructor(assembly, programType, assemblyLock, executingAssembly, loadedAssemblies, currentDomain, eventHandler, assemblyResolve, getExecutingAssembly, resolveMethod);

            //Now create a type to hold the entry point
#if USE_FRIENDLY_NAMING
            TypeDefinition entryType = new TypeDefinition("Program",
                                            "NCloak.Bootstrapper",
                                            TypeAttributes.NotPublic | TypeAttributes.Class |
                                            TypeAttributes.AutoClass | TypeAttributes.AnsiClass |
                                            TypeAttributes.BeforeFieldInit,
                                            assembly.Import(typeof(object)));
#else
            TypeDefinition entryType = new TypeDefinition(context.NameManager.GenerateName(NamingTable.Type),
                                                        context.NameManager.GenerateName(NamingTable.Type),
                                                        TypeAttributes.NotPublic | TypeAttributes.Class |
                                                        TypeAttributes.AutoClass | TypeAttributes.AnsiClass |
                                                        TypeAttributes.BeforeFieldInit,
                                                        assembly.Import(typeof(object)));
#endif
            assembly.MainModule.Types.Add(entryType);

            //Create a default constructor
            var ctor = assembly.CreateDefaultConstructor(entryType);
            ctor.MaxStack = 8;
            var il = ctor.CilWorker;
            InjectAntiReflectorCode(il, il.Create(OpCodes.Ldarg_0));
            var objectCtor = assembly.Import(typeof (object).GetConstructor(Type.EmptyTypes));
            il.Append(il.Create(OpCodes.Call, objectCtor));
            il.Append(OpCodes.Ret);

            //Create an entry point
            var mainMethod = BuildMainMethod(assembly, context, programType, getExecutingAssembly, currentDomain, eventHandler, assemblyResolve, resolveMethod, startMethod);
            entryType.Methods.Add(mainMethod);
        }

        /// <summary>
        /// Builds the load assemblies method.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The assembly.</param>
        /// <param name="executingAssembly">The executing assembly.</param>
        /// <param name="loadAssembly">The load assembly.</param>
        /// <returns></returns>
        private static MethodDefinition BuildLoadAssembliesMethod(ICloakContext context, AssemblyDefinition assembly, FieldReference executingAssembly, MethodReference loadAssembly)
        {
#if USE_FRIENDLY_NAMING
            MethodDefinition method = new MethodDefinition("LoadAssemblies",
                                               MethodAttributes.Private | MethodAttributes.HideBySig, assembly.Import(typeof(void)));
#else
            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                               MethodAttributes.Private | MethodAttributes.HideBySig, assembly.Import(typeof(void)));
#endif
            method.Body.InitLocals = true;
            method.Body.MaxStack = 2;
            method.AddLocal(assembly, typeof (string)); //Resource name
            method.AddLocal(assembly, typeof (string[])); //Foreach temp
            method.AddLocal(assembly, typeof (int)); //Loop counter
            method.AddLocal(assembly, typeof(bool));

            //Build the body
            var il = method.Body.CilWorker;
            InjectAntiReflectorCode(il, il.Create(OpCodes.Nop));
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Ldfld, executingAssembly));

            //Get the resources - foreach get's converted to a standard loop
            var resourcesMethod = typeof (Assembly).GetMethod("GetManifestResourceNames");
            il.Append(il.Create(OpCodes.Callvirt, assembly.Import(resourcesMethod)));
            il.Append(OpCodes.Stloc_1);

            //Initialise the loop counter
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Stloc_2);

            //For loop comparison (well create somewhere to jump to)
            var loopComparisonStart = il.Create(OpCodes.Ldloc_2);

            //Check the comparison now
            il.Append(il.Create(OpCodes.Br_S, loopComparisonStart));
            var startCode = il.Create(OpCodes.Ldloc_1);
            il.Append(startCode);
            il.Append(OpCodes.Ldloc_2);
            il.Append(OpCodes.Ldelem_Ref);
            il.Append(OpCodes.Stloc_0);
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldloc_0);

            //Make sure it doesn't end with .v0 or .e
            il.Append(il.Create(OpCodes.Ldstr, ".v0"));
            var endsWith = assembly.Import(typeof (String).GetMethod("EndsWith", new[] {typeof (string)}));
            il.Append(il.Create(OpCodes.Callvirt, endsWith));

            //Jump out as need be
            var load0 = il.Create(OpCodes.Ldc_I4_0);
            il.Append(il.Create(OpCodes.Brtrue_S, load0));
            il.Append(OpCodes.Ldloc_0);
            
            //Check the .e
            il.Append(il.Create(OpCodes.Ldstr, ".e"));
            il.Append(il.Create(OpCodes.Callvirt, endsWith));
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            var storeTempBool = il.Create(OpCodes.Stloc_3);
            il.Append(il.Create(OpCodes.Br_S, storeTempBool));
            il.Append(load0);
            il.Append(storeTempBool);
            il.Append(OpCodes.Ldloc_3);
            var loadArg0 = il.Create(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Brtrue_S, loadArg0));
            var loadLoopCounter = il.Create(OpCodes.Ldloc_2);
            il.Append(il.Create(OpCodes.Br_S, loadLoopCounter));
            il.Append(loadArg0);
            il.Append(OpCodes.Ldloc_0);

            //Finally load the assembly
            il.Append(il.Create(OpCodes.Call, loadAssembly));
            il.Append(OpCodes.Pop);
            il.Append(OpCodes.Nop);
            
            //Loop counter
            il.Append(loadLoopCounter);
            il.Append(OpCodes.Ldc_I4_1);
            il.Append(OpCodes.Add);
            il.Append(OpCodes.Stloc_2);

            //Loop comparison
            il.Append(loopComparisonStart);
            il.Append(OpCodes.Ldloc_1);
            il.Append(OpCodes.Ldlen);
            il.Append(OpCodes.Conv_I4);
            il.Append(OpCodes.Clt);
            //Store the result
            il.Append(OpCodes.Stloc_3);
            //Load it and compare
            il.Append(OpCodes.Ldloc_3);
            il.Append(il.Create(OpCodes.Brtrue_S, startCode));
            il.Append(OpCodes.Ret);
            return method;
        }

        /// <summary>
        /// Builds the load assembly method.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The assembly.</param>
        /// <param name="loadedAssemblies">The loaded assemblies.</param>
        /// <param name="executingAssembly">The executing assembly.</param>
        /// <param name="decryptData">The decrypt data.</param>
        /// <param name="hashData">The hash data.</param>
        /// <param name="passwordKey">The password key.</param>
        /// <param name="salt">The salt.</param>
        /// <returns></returns>
        private static MethodDefinition BuildLoadAssemblyMethod(ICloakContext context, AssemblyDefinition assembly, FieldReference loadedAssemblies, FieldReference executingAssembly, MethodReference decryptData, MethodReference hashData, string passwordKey, Guid salt)
        {
#if USE_FRIENDLY_NAMING
            MethodDefinition method = new MethodDefinition("LoadAssembly",
                       MethodAttributes.Private | MethodAttributes.HideBySig, assembly.Import(typeof(Assembly)));
#else
            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                   MethodAttributes.Private | MethodAttributes.HideBySig, assembly.Import(typeof(Assembly)));
#endif

            //Declare the resource parameter
            method.Parameters.Add(new ParameterDefinition(assembly.Import(typeof(string))));

            method.Body.InitLocals = true;
            method.Body.MaxStack = 4;
            method.AddLocal(assembly, typeof(string)); //Hash
            method.AddLocal(assembly, typeof(Stream)); //Stream for loading
            method.AddLocal(assembly, typeof(byte[])); //Hash data
            method.AddLocal(assembly, typeof(byte[])); //Data
            var password = method.AddLocal(assembly, typeof(Rfc2898DeriveBytes)); //Password
            var keyBytes = method.AddLocal(assembly, typeof(byte[])); //Key bytes
            var initVector = method.AddLocal(assembly, typeof(byte[])); //Init vector
            var rawAssembly = method.AddLocal(assembly, typeof(byte[])); //Assembly raw bytes
            var actualAssembly = method.AddLocal(assembly, typeof(Assembly)); //Actual Assembly
            var tempAssembly = method.AddLocal(assembly, typeof(Assembly)); //Temp Assembly
            var tempBool = method.AddLocal(assembly, typeof(bool)); //Temp bool

            //Build the body
            var il = method.Body.CilWorker;
            InjectAntiReflectorCode(il, il.Create(OpCodes.Nop));

#if VERBOSE_OUTPUT
            DebugLine(assembly, il, "Loading ", il.Create(OpCodes.Ldarg_1));
#endif

            //Check the cache first
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Ldfld, loadedAssemblies));
            il.Append(OpCodes.Ldarg_1);
            var containsKey = typeof (Dictionary<string, Assembly>).GetMethod("ContainsKey");
            il.Append(il.Create(OpCodes.Callvirt, assembly.Import(containsKey)));
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            //Jump if we don't have it, other wise return it
            var startFindType = il.Create(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Brtrue_S, startFindType));
            
            //Otherwise return the cached assembly
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Ldfld, loadedAssemblies));
            il.Append(OpCodes.Ldarg_1);
            //We are retrieving the name item
            var getItem = typeof (Dictionary<string, Assembly>).GetProperty("Item");
            il.Append(il.Create(OpCodes.Callvirt, assembly.Import(getItem.GetGetMethod())));
            //Store the variable in our return arg
            il.Append(il.Create(OpCodes.Stloc_S, tempAssembly));
            //Branch to our return routine
            var returnSequence = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Br, returnSequence));

            //Back to finding our type
            //Let's check the hash first
            il.Append(startFindType);
            il.Append(il.Create(OpCodes.Ldfld, executingAssembly));
            il.Append(OpCodes.Ldarg_1);
            il.Append(il.Create(OpCodes.Ldstr, ".v0"));
            //Concat the arg1 (resourceName) and v0
            var concat = assembly.Import(typeof (String).GetMethod("Concat", new[] {typeof (string), typeof (string)}));
            il.Append(il.Create(OpCodes.Call, concat));
            //Now get this from the manifest
            var getManifest = assembly.Import(typeof (Assembly).GetMethod("GetManifestResourceStream", new[] {typeof (string)}));
            il.Append(il.Create(OpCodes.Callvirt, getManifest));
            //Store the result in our temp local
            il.Append(OpCodes.Stloc_1);
            var try1Start = il.Create(OpCodes.Nop);
            il.Append(try1Start);
            //Make sure it's not null
            il.Append(OpCodes.Ldloc_1);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            //Return null if it is
            var startDeclareArray = il.Create(OpCodes.Ldloc_1);
            il.Append(il.Create(OpCodes.Brtrue_S, startDeclareArray));
            //Load null into our return variable
            il.Append(OpCodes.Ldnull); 
            il.Append(il.Create(OpCodes.Stloc_S, tempAssembly));
            //Jump to the return routine
            il.Append(il.Create(OpCodes.Leave, returnSequence)); //Leave as in try
            //Carry on and declare a buffer
            il.Append(startDeclareArray);
            var getLength = assembly.Import(typeof (Stream).GetProperty("Length").GetGetMethod());
            il.Append(il.Create(OpCodes.Callvirt, getLength));
            il.Append(OpCodes.Conv_Ovf_I);
            il.Append(il.Create(OpCodes.Newarr, assembly.Import(typeof(byte))));
            //Now read it in
            il.Append(OpCodes.Stloc_2);
            il.Append(OpCodes.Ldloc_1);
            il.Append(OpCodes.Ldloc_2);
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ldloc_2);
            il.Append(OpCodes.Ldlen);
            il.Append(OpCodes.Conv_I4);
            var readMethod =
                assembly.Import(typeof (Stream).GetMethod("Read", new[] {typeof (byte[]), typeof (int), typeof (int)}));
            il.Append(il.Create(OpCodes.Callvirt, readMethod));

            //Get the result
            il.Append(OpCodes.Pop);
            il.Append(OpCodes.Ldloc_2);
            //Convert it to base 64
            var base64String = assembly.Import(typeof (Convert).GetMethod("ToBase64String", new[] {typeof (byte[])}));
            il.Append(il.Create(OpCodes.Call, base64String));
            //Get the result
            il.Append(OpCodes.Stloc_0);
            il.Append(OpCodes.Nop);
            
            //Read the assembly
            var startReadAssembly = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Leave_S, startReadAssembly));
            
            //Dispose of appropriately
            var try1End = il.Create(OpCodes.Ldloc_1);
            il.Append(try1End);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            var endFinally1 = il.Create(OpCodes.Endfinally);
            il.Append(il.Create(OpCodes.Brtrue_S, endFinally1));
            il.Append(OpCodes.Ldloc_1);
            //Dispose
            var disposeMethod = assembly.Import(typeof (IDisposable).GetMethod("Dispose"));
            il.Append(il.Create(OpCodes.Callvirt, disposeMethod));
            il.Append(OpCodes.Nop);
            il.Append(endFinally1);
            
            //Start the read assembly
            il.Append(startReadAssembly);
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Ldfld, executingAssembly));
            il.Append(OpCodes.Ldarg_1);
            il.Append(il.Create(OpCodes.Callvirt, getManifest));
            il.Append(OpCodes.Stloc_1);
            var try2Start = il.Create(OpCodes.Nop);
            il.Append(try2Start);
            il.Append(OpCodes.Ldloc_1);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            //Check if it is null
            var startReadAssembly2 = il.Create(OpCodes.Ldloc_1);
            il.Append(il.Create(OpCodes.Brtrue_S, startReadAssembly2));
            il.Append(OpCodes.Ldnull);
            il.Append(il.Create(OpCodes.Stloc_S, tempAssembly));
            il.Append(il.Create(OpCodes.Leave, returnSequence)); //Leave as in try
            //Read the assembly
            il.Append(startReadAssembly2);
            il.Append(il.Create(OpCodes.Callvirt, getLength));
            il.Append(OpCodes.Conv_Ovf_I);
            il.Append(il.Create(OpCodes.Newarr, assembly.Import(typeof(byte))));
            //Now read it in
            il.Append(OpCodes.Stloc_3);
            il.Append(OpCodes.Ldloc_1);
            il.Append(OpCodes.Ldloc_3);
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ldloc_3);
            il.Append(OpCodes.Ldlen);
            il.Append(OpCodes.Conv_I4);
            il.Append(il.Create(OpCodes.Callvirt, readMethod));

            //Get the result and clean up
            il.Append(OpCodes.Pop);
            il.Append(OpCodes.Nop);

            var startCheck = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Leave_S, startCheck));
            var try2End = il.Create(OpCodes.Ldloc_1);
            il.Append(try2End);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            var endFinally2 = il.Create(OpCodes.Endfinally);
            il.Append(il.Create(OpCodes.Brtrue_S, endFinally2));
            il.Append(OpCodes.Ldloc_1);
            //Dispose
            il.Append(il.Create(OpCodes.Callvirt, disposeMethod)); 
            il.Append(OpCodes.Nop);
            il.Append(endFinally2);
            il.Append(startCheck);

            //We need to load in both the salt, and the password key
            il.Append(il.Create(OpCodes.Ldstr, passwordKey));
            il.Append(il.Create(OpCodes.Ldstr, Convert.ToBase64String(salt.ToByteArray())));
            var fromBase64String = assembly.Import(typeof (Convert).GetMethod("FromBase64String"));
            il.Append(il.Create(OpCodes.Call, fromBase64String));
            //Set these into an rfc 2898 object
            il.Append(OpCodes.Ldc_I4_2);
            //RFC2898 ctor
            var rfcCtor =
                assembly.Import(
                    typeof (Rfc2898DeriveBytes).GetConstructor(new[] {typeof (string), typeof (byte[]), typeof (int)}));
            il.Append(il.Create(OpCodes.Newobj, rfcCtor));
            //Store this object
            il.Append(il.Create(OpCodes.Stloc_S, password));

            //Get the key bytes
            il.Append(il.Create(OpCodes.Ldloc_S, password));
            il.Append(il.Create(OpCodes.Ldc_I4_S, (sbyte)0x20));
            var getBytes = assembly.Import(typeof (DeriveBytes).GetMethod("GetBytes", new[] {typeof (int)}));
            il.Append(il.Create(OpCodes.Callvirt, getBytes));
            il.Append(il.Create(OpCodes.Stloc_S, keyBytes));

            //Init vector
            il.Append(il.Create(OpCodes.Ldloc_S, password));
            il.Append(il.Create(OpCodes.Ldc_I4_S, (sbyte)0x10));
            il.Append(il.Create(OpCodes.Callvirt, getBytes));
            il.Append(il.Create(OpCodes.Stloc_S, initVector));

            //Now decrypt the data
            il.Append(OpCodes.Ldloc_3);
            il.Append(il.Create(OpCodes.Ldloc_S, keyBytes));
            il.Append(il.Create(OpCodes.Ldloc_S, initVector));
            il.Append(il.Create(OpCodes.Call, decryptData));
            il.Append(il.Create(OpCodes.Stloc_S, rawAssembly));

            //Get the "real" hash
            il.Append(OpCodes.Ldloc_0);
            il.Append(il.Create(OpCodes.Ldloc_S, rawAssembly));
            il.Append(il.Create(OpCodes.Call, hashData));
            //Run the inequality operator
            var inequality =
                assembly.Import(typeof (String).GetMethod("op_Inequality", new[] {typeof (string), typeof (string)}));
            il.Append(il.Create(OpCodes.Call, inequality));
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));

            //Return null if it's not a match
            var startLoadAssembly = il.Create(OpCodes.Ldloc_S, rawAssembly);
            il.Append(il.Create(OpCodes.Brtrue_S, startLoadAssembly));
            il.Append(OpCodes.Ldnull);
            il.Append(il.Create(OpCodes.Stloc_S, tempAssembly));
            il.Append(il.Create(OpCodes.Br_S, returnSequence));
            il.Append(startLoadAssembly);

            var assemblyLoad = assembly.Import(typeof (Assembly).GetMethod("Load", new[] {typeof (byte[])}));
            il.Append(il.Create(OpCodes.Call, assemblyLoad));
            il.Append(il.Create(OpCodes.Stloc_S, actualAssembly));
            
            //Check if it is null
            il.Append(il.Create(OpCodes.Ldloc_S, actualAssembly));
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            var startCache = il.Create(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Brtrue_S, startCache));
            il.Append(OpCodes.Ldnull);
            il.Append(il.Create(OpCodes.Stloc_S, tempAssembly));
            il.Append(il.Create(OpCodes.Br_S, returnSequence));

            //Cache before returning
            il.Append(startCache);
            il.Append(il.Create(OpCodes.Ldfld, loadedAssemblies));
            il.Append(OpCodes.Ldarg_1);
            il.Append(il.Create(OpCodes.Ldloc_S, actualAssembly));
            var addMethod = assembly.Import(typeof (Dictionary<string, Assembly>).GetMethod("Add"));            
            il.Append(il.Create(OpCodes.Callvirt, addMethod));
            il.Append(OpCodes.Nop);
            //Move it to our temp return var
            il.Append(il.Create(OpCodes.Ldloc_S, actualAssembly));
            il.Append(il.Create(OpCodes.Stloc_S, tempAssembly));
            il.Append(il.Create(OpCodes.Br_S, returnSequence)); //Need this?
            //Return sequence
            il.Append(returnSequence);
#if VERBOSE_OUTPUT
            DebugLine(assembly, il, "Successfully loaded");
#endif
            /*
            il.Append(il.Create(OpCodes.Ldstr, "Load assembly "));
            il.Append(OpCodes.Ldarg_1);
            il.Append(il.Create(OpCodes.Call, concat));
            il.Append(OpCodes.Stloc_0);
            il.Append(OpCodes.Ldloc_0);
            var wl = assembly.Import(typeof(Console).GetMethod("WriteLine", new[] { typeof(string) }));
            il.Append(il.Create(OpCodes.Call, wl));
             */

            il.Append(il.Create(OpCodes.Ldloc_S, tempAssembly));
            il.Append(OpCodes.Ret);

            //Last but not least, add our try/finally's (i.e. usings)
            ExceptionHandler finally1 = new ExceptionHandler(ExceptionHandlerType.Finally);
            finally1.TryStart = try1Start;
            finally1.TryEnd = try1End;
            finally1.HandlerStart = try1End;
            finally1.HandlerEnd = startReadAssembly;
            method.Body.ExceptionHandlers.Add(finally1);
            ExceptionHandler finally2 = new ExceptionHandler(ExceptionHandlerType.Finally);
            finally2.TryStart = try2Start;
            finally2.TryEnd = try2End;
            finally2.HandlerStart = try2End;
            finally2.HandlerEnd = startCheck;
            method.Body.ExceptionHandlers.Add(finally2);
            return method;
        }

        /// <summary>
        /// Builds the load type method.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The assembly.</param>
        /// <param name="loadAssembly">The load assembly.</param>
        /// <returns></returns>
        private static MethodDefinition BuildLoadTypeMethod(ICloakContext context, AssemblyDefinition assembly, MethodReference loadAssembly)
        {
#if USE_FRIENDLY_NAMING
            MethodDefinition method = new MethodDefinition("LoadType",
                       MethodAttributes.Private | MethodAttributes.HideBySig, assembly.Import(typeof(Type)));
#else
            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                   MethodAttributes.Private | MethodAttributes.HideBySig, assembly.Import(typeof(Type)));
#endif

            //Declare the resource parameter
            method.Parameters.Add(new ParameterDefinition(assembly.Import(typeof(string))));
            method.Parameters.Add(new ParameterDefinition(assembly.Import(typeof(string))));
            
            method.Body.InitLocals = true;
            method.Body.MaxStack = 2;
            method.AddLocal(assembly, typeof(Assembly)); //Assembly
            method.AddLocal(assembly, typeof(Type)); //Temp variable for return type
            method.AddLocal(assembly, typeof(bool)); //Temp variable for comparison

            //Start with injection
            var il = method.Body.CilWorker;
            InjectAntiReflectorCode(il, il.Create(OpCodes.Nop));

            //Start the code
            il.Append(OpCodes.Ldarg_0);
            il.Append(OpCodes.Ldarg_1);
            il.Append(il.Create(OpCodes.Call, loadAssembly));
            il.Append(OpCodes.Stloc_0);
            il.Append(OpCodes.Ldloc_0);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            il.Append(OpCodes.Stloc_2);
            il.Append(OpCodes.Ldloc_2);

            //Get type
            var getType = il.Create(OpCodes.Ldloc_0);
            il.Append(il.Create(OpCodes.Brtrue_S, getType));
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Stloc_1);

            //Prepare to return the temp type
            var returnType = il.Create(OpCodes.Ldloc_1);

            //Jump to it
            il.Append(il.Create(OpCodes.Br_S, returnType));
            il.Append(getType);
            il.Append(OpCodes.Ldarg_2);
            var getTypeMethod = typeof (Assembly).GetMethod("GetType", new[] {typeof (string)});
            il.Append(il.Create(OpCodes.Callvirt, assembly.Import(getTypeMethod)));
            il.Append(OpCodes.Stloc_1);
            il.Append(il.Create(OpCodes.Br_S, returnType));
            il.Append(returnType);
            il.Append(OpCodes.Ret);
            return method;
        }

        /// <summary>
        /// Builds the hash method.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The assembly.</param>
        /// <returns></returns>
        private static MethodDefinition BuildHashMethod(ICloakContext context, AssemblyDefinition assembly)
        {
#if USE_FRIENDLY_NAMING
            MethodDefinition method = new MethodDefinition("Hash",
                                               MethodAttributes.Private | MethodAttributes.HideBySig |
                                               MethodAttributes.Static, assembly.Import(typeof(string)));
#else
            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                               MethodAttributes.Private | MethodAttributes.HideBySig |
                                               MethodAttributes.Static, assembly.Import(typeof(string)));
#endif
            method.Parameters.Add(new ParameterDefinition(assembly.Import(typeof(byte[]))));
            method.Body.InitLocals = true;
            method.Body.MaxStack = 2;
            method.AddLocal(assembly, typeof(SHA256));
            method.AddLocal(assembly, typeof(string));

            //Easy method to output
            var il = method.Body.CilWorker;

            //Inject some anti reflector stuff
            InjectAntiReflectorCode(il, il.Create(OpCodes.Nop));

            //Create the SHA256 object
            var sha256Create = typeof(SHA256).GetMethod("Create", Type.EmptyTypes);
            il.Append(il.Create(OpCodes.Call, assembly.Import(sha256Create)));
            il.Append(OpCodes.Stloc_0);
            il.Append(OpCodes.Ldloc_0);
            il.Append(OpCodes.Ldarg_0);
            //Call the compute method
            var compute = typeof(HashAlgorithm).GetMethod("ComputeHash", new[] { typeof(byte[]) });
            il.Append(il.Create(OpCodes.Callvirt, assembly.Import(compute)));
            //Finally, convert to base 64
            var toBase64 = typeof(Convert).GetMethod("ToBase64String", new[] { typeof(byte[]) });
            il.Append(il.Create(OpCodes.Call, assembly.Import(toBase64)));
            il.Append(OpCodes.Stloc_1);
            var retVar = il.Create(OpCodes.Ldloc_1);
            il.Append(il.Create(OpCodes.Br_S, retVar));
            il.Append(retVar);
            il.Append(OpCodes.Ret);
            return method;
        }

        /// <summary>
        /// Builds the decrypt method.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The assembly.</param>
        /// <returns></returns>
        private static MethodDefinition BuildDecryptMethod(ICloakContext context, AssemblyDefinition assembly)
        {
            var byteArrayType = assembly.Import(typeof (byte[]));
#if USE_FRIENDLY_NAMING
            MethodDefinition method = new MethodDefinition("DecryptData",
                                                           MethodAttributes.Private | MethodAttributes.HideBySig |
                                                           MethodAttributes.Static, byteArrayType);
#else
            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                                           MethodAttributes.Private | MethodAttributes.HideBySig |
                                                           MethodAttributes.Static, assembly.Import(typeof(byte[])));
#endif
            method.Body.InitLocals = true;
            method.Body.MaxStack = 5;
            method.Parameters.Add(new ParameterDefinition(byteArrayType));
            method.Parameters.Add(new ParameterDefinition(byteArrayType));
            method.Parameters.Add(new ParameterDefinition(byteArrayType));

            //Declare the locals - first four have quick reference
            method.AddLocal(assembly, typeof(Rijndael));
            method.AddLocal(assembly, typeof(ICryptoTransform));
            method.AddLocal(assembly, typeof(MemoryStream));
            method.AddLocal(assembly, typeof(CryptoStream));
            var paddedPlain = method.AddLocal(assembly, typeof(byte[]));
            var length = method.AddLocal(assembly, typeof(int));
            var plain = method.AddLocal(assembly, typeof(byte[]));
            var returnArray = method.AddLocal(assembly, typeof(byte[]));
            var inferredBool = method.AddLocal(assembly, typeof(bool));

            //Add the body
            var il = method.Body.CilWorker;

            //Inject anti reflector code
            InjectAntiReflectorCode(il, il.Create(OpCodes.Nop));

            //Start the body
            var rijndaelCreate = typeof(Rijndael).GetMethod("Create", Type.EmptyTypes);
            il.Append(il.Create(OpCodes.Call, assembly.Import(rijndaelCreate)));
            il.Append(OpCodes.Stloc_0);
            il.Append(OpCodes.Ldloc_0);
            il.Append(OpCodes.Ldc_I4_1);
            var symmetricAlgMode = typeof(SymmetricAlgorithm).GetProperty("Mode");
            il.Append(il.Create(OpCodes.Callvirt, assembly.Import(symmetricAlgMode.GetSetMethod())));
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldloc_0);
            il.Append(OpCodes.Ldarg_1);
            il.Append(OpCodes.Ldarg_2);
            var createDecryptor = typeof(SymmetricAlgorithm).GetMethod("CreateDecryptor",
                                                                        new[] { typeof(byte[]), typeof(byte[]) });
            il.Append(il.Create(OpCodes.Callvirt, assembly.Import(createDecryptor)));
            il.Append(OpCodes.Stloc_1);
            var startOfThirdTry = il.Create(OpCodes.Nop);
            il.Append(startOfThirdTry);
            il.Append(OpCodes.Ldarg_0);

            //New memory stream
            var memoryStreamCtor = typeof(MemoryStream).GetConstructor(new [] { typeof(byte[]) });
            il.Append(il.Create(OpCodes.Newobj, assembly.Import(memoryStreamCtor)));
            il.Append(OpCodes.Stloc_2);
            var startOfSecondTry = il.Create(OpCodes.Nop);
            il.Append(startOfSecondTry);
            il.Append(OpCodes.Ldloc_2);
            il.Append(OpCodes.Ldloc_1);
            il.Append(OpCodes.Ldc_I4_0);

            //New crypto stream
            var cryptoStreamCtor =
                typeof(CryptoStream).GetConstructor(new[]
                                                                     {
                                                                         typeof (Stream), typeof (ICryptoTransform),
                                                                         typeof (CryptoStreamMode)
                                                                     });
            il.Append(il.Create(OpCodes.Newobj, assembly.Import(cryptoStreamCtor)));
            il.Append(OpCodes.Stloc_3);
            var startOfFirstTry = il.Create(OpCodes.Nop);
            il.Append(startOfFirstTry);
            il.Append(OpCodes.Ldarg_0);
            il.Append(OpCodes.Ldlen);
            il.Append(OpCodes.Conv_I4);
            il.Append(il.Create(OpCodes.Newarr, assembly.Import(typeof(byte))));
            il.Append(il.Create(OpCodes.Stloc_S, paddedPlain));
            il.Append(OpCodes.Ldloc_3);
            il.Append(il.Create(OpCodes.Ldloc_S, paddedPlain));
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(il.Create(OpCodes.Ldloc_S, paddedPlain));
            il.Append(OpCodes.Ldlen);
            il.Append(OpCodes.Conv_I4);

            //Get the read method and read into this byte array (full length)
            var read = typeof(Stream).GetMethod("Read", new[] { typeof(byte[]), typeof(int), typeof(int) });
            il.Append(il.Create(OpCodes.Callvirt, assembly.Import(read)));
            il.Append(il.Create(OpCodes.Stloc_S, length));
            il.Append(il.Create(OpCodes.Ldloc_S, length));
            il.Append(il.Create(OpCodes.Newarr, assembly.Import(typeof(byte))));
            il.Append(il.Create(OpCodes.Stloc_S, plain));
            il.Append(il.Create(OpCodes.Ldloc_S, paddedPlain));
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(il.Create(OpCodes.Ldloc_S, plain));
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(il.Create(OpCodes.Ldloc_S, length));

            //Array::Copy
            var arrayCopy = typeof(Array).GetMethod("Copy",
                                                     new[]
                                                                     {
                                                                         typeof (Array), typeof (int), typeof (Array), typeof (int),
                                                                         typeof (int)
                                                                     });
            il.Append(il.Create(OpCodes.Call, assembly.Import(arrayCopy)));
            il.Append(OpCodes.Nop);//padding
            il.Append(il.Create(OpCodes.Ldloc_S, plain));
            il.Append(il.Create(OpCodes.Stloc_S, returnArray));

            //Now it gets a bit confusing as we need to do a few jumps (OO and all...)
            var endNop = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Leave_S, endNop));

            //First finally
            var firstStartFinally = il.Create(OpCodes.Ldloc_3);
            il.Append(firstStartFinally);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, inferredBool));
            il.Append(il.Create(OpCodes.Ldloc_S, inferredBool));
            var firstEndFinally = il.Create(OpCodes.Endfinally);
            il.Append(il.Create(OpCodes.Brtrue_S, firstEndFinally));
            il.Append(OpCodes.Ldloc_3);
            //Get the disposable virtual method
            var dispose = assembly.Import(typeof(IDisposable).GetMethod("Dispose"));
            il.Append(il.Create(OpCodes.Callvirt, dispose));
            il.Append(OpCodes.Nop);
            il.Append(firstEndFinally);

            //Second finally
            var secondStartFinally = il.Create(OpCodes.Ldloc_2);
            il.Append(secondStartFinally);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, inferredBool));
            il.Append(il.Create(OpCodes.Ldloc_S, inferredBool));
            var secondEndFinally = il.Create(OpCodes.Endfinally);
            il.Append(il.Create(OpCodes.Brtrue_S, secondEndFinally));
            il.Append(OpCodes.Ldloc_2);
            il.Append(il.Create(OpCodes.Callvirt, dispose));
            il.Append(OpCodes.Nop);
            il.Append(secondEndFinally);

            //Third finally
            var thirdStartFinally = il.Create(OpCodes.Ldloc_1);
            il.Append(thirdStartFinally);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, inferredBool));
            il.Append(il.Create(OpCodes.Ldloc_S, inferredBool));
            var thirdEndFinally = il.Create(OpCodes.Endfinally);
            il.Append(il.Create(OpCodes.Brtrue_S, thirdEndFinally));
            il.Append(OpCodes.Ldloc_1);
            il.Append(il.Create(OpCodes.Callvirt, dispose));
            il.Append(OpCodes.Nop);
            il.Append(thirdEndFinally);

            //Clean up and return
            il.Append(endNop);
            il.Append(il.Create(OpCodes.Ldloc_S, returnArray));
            il.Append(OpCodes.Ret);

            //Add the try/finally handlers
            var handler1 = new ExceptionHandler(ExceptionHandlerType.Finally);
            handler1.TryStart = startOfFirstTry;
            handler1.TryEnd = firstStartFinally;
            handler1.HandlerStart = firstStartFinally;
            handler1.HandlerEnd = secondStartFinally;
            method.Body.ExceptionHandlers.Add(handler1);

            var handler2 = new ExceptionHandler(ExceptionHandlerType.Finally);
            handler2.TryStart = startOfSecondTry;
            handler2.TryEnd = secondStartFinally;
            handler2.HandlerStart = secondStartFinally;
            handler2.HandlerEnd = thirdStartFinally;
            method.Body.ExceptionHandlers.Add(handler2);

            var handler3 = new ExceptionHandler(ExceptionHandlerType.Finally);
            handler3.TryStart = startOfThirdTry;
            handler3.TryEnd = thirdStartFinally;
            handler3.HandlerStart = thirdStartFinally;
            handler3.HandlerEnd = endNop;
            method.Body.ExceptionHandlers.Add(handler3);

            return method;
        }

        /// <summary>
        /// Builds the resolve method.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The definition.</param>
        /// <param name="assemblyLock">The assembly lock.</param>
        /// <param name="assembliesLoaded">The assemblies loaded.</param>
        /// <param name="loadAssemblies">The load assemblies.</param>
        /// <returns></returns>
        private static MethodDefinition BuildResolveMethod(ICloakContext context, AssemblyDefinition assembly, FieldReference assemblyLock, FieldReference assembliesLoaded, MethodReference loadAssemblies)
        {
#if USE_FRIENDLY_NAMING
            MethodDefinition method = new MethodDefinition("ResolveAssembly",
                                   MethodAttributes.Public | MethodAttributes.HideBySig, assembly.Import(typeof(Assembly)));
#else
            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                   MethodAttributes.Public | MethodAttributes.HideBySig, assembly.Import(typeof(Assembly)));
#endif

            //Declare the resource parameter
            method.Parameters.Add(new ParameterDefinition("sender", 0, Mono.Cecil.ParameterAttributes.None, assembly.Import(typeof(object))));
            method.Parameters.Add(new ParameterDefinition(assembly.Import(typeof(ResolveEventArgs))));
            
            method.Body.InitLocals = true;
            method.Body.MaxStack = 2;
            method.AddLocal(assembly, typeof(Assembly[])); //currentAssemblies
            method.AddLocal(assembly, typeof(Assembly));   //a
            method.AddLocal(assembly, typeof(Assembly));   //temp assembly
            method.AddLocal(assembly, typeof (object));    //temp object
            var tempBool = method.AddLocal(assembly, typeof (bool)); //temp bool
            var tempAssemblyArray = method.AddLocal(assembly, typeof(Assembly[])); //temp assembly array
            var tempInt = method.AddLocal(assembly, typeof (int)); //temp int
        
            //Get the il builder
            var il = method.Body.CilWorker;

            //Inject the anti reflector code
            InjectAntiReflectorCode(il, il.Create(OpCodes.Nop));
#if VERBOSE_OUTPUT
            DebugLine(assembly, il, "Resolving");
#endif

            //Start a lock
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Ldfld, assemblyLock));
            il.Append(OpCodes.Dup);
            il.Append(OpCodes.Stloc_3);
            var monitorEnter = assembly.Import(typeof (Monitor).GetMethod("Enter", new[] {typeof (object)}));
            il.Append(il.Create(OpCodes.Call, monitorEnter));
            il.Append(OpCodes.Nop);
            var tryStart = il.Create(OpCodes.Nop);
            il.Append(tryStart);
            //Check if the assemblies are loaded
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Ldfld, assembliesLoaded));
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));

            //If it is set, skip to searching the current appdomain
            var startSearchingCurrentAppDomain = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Brtrue_S, startSearchingCurrentAppDomain));
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldarg_0);
            il.Append(OpCodes.Ldc_I4_1);
            il.Append(il.Create(OpCodes.Stfld, assembliesLoaded));
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Call, loadAssemblies));
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Nop);
            il.Append(startSearchingCurrentAppDomain);
            //After the finally
            var afterFinally = il.Create(OpCodes.Nop);
            //Exit the finally
            il.Append(il.Create(OpCodes.Leave_S, afterFinally));
            //We're in the finally now
            var finallyStart = il.Create(OpCodes.Ldloc_3);
            il.Append(finallyStart);
            var monitorExit = assembly.Import(typeof (Monitor).GetMethod("Exit", new[] {typeof (object)}));
            il.Append(il.Create(OpCodes.Call, monitorExit));
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Endfinally);
            il.Append(afterFinally);

            //Get the current domains assemblies
            var currentDomain = assembly.Import(typeof (AppDomain).GetProperty("CurrentDomain").GetGetMethod());
            il.Append(il.Create(OpCodes.Call, currentDomain));
            var getAssemblies = assembly.Import(typeof (AppDomain).GetMethod("GetAssemblies"));
            il.Append(il.Create(OpCodes.Callvirt, getAssemblies));
            il.Append(OpCodes.Stloc_0);
            il.Append(OpCodes.Nop);

            //Lets start a loop in lieu of foreach
            il.Append(OpCodes.Ldloc_0);
            il.Append(il.Create(OpCodes.Stloc_S, tempAssemblyArray));
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(il.Create(OpCodes.Stloc_S, tempInt));

            //Loop Condition
            var loopCondition = il.Create(OpCodes.Ldloc_S, tempInt);
            il.Append(il.Create(OpCodes.Br_S, loopCondition));
            var startLoopBody = il.Create(OpCodes.Ldloc_S, tempAssemblyArray);
            il.Append(startLoopBody);
            il.Append(il.Create(OpCodes.Ldloc_S, tempInt));
            il.Append(OpCodes.Ldelem_Ref);
            il.Append(OpCodes.Stloc_1);
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldloc_1);

            //Check the name
            var getFullName = assembly.Import(typeof (Assembly).GetProperty("FullName").GetGetMethod());
            il.Append(il.Create(OpCodes.Callvirt, getFullName));
            il.Append(OpCodes.Ldarg_2);
            var getName = assembly.Import(typeof (ResolveEventArgs).GetProperty("Name").GetGetMethod());
            il.Append(il.Create(OpCodes.Callvirt, getName));
            //Check if equal
            var stringEquality =
                assembly.Import(typeof (String).GetMethod("op_Equality", new[] {typeof (string), typeof (string)}));
            il.Append(il.Create(OpCodes.Call, stringEquality));
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));

            //Branch to increment block if necessary
            var incrementBlock = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Brtrue_S, incrementBlock));
            il.Append(OpCodes.Ldloc_1);
            il.Append(OpCodes.Stloc_2);
            var returnBlock = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Leave_S, returnBlock));
            //i++
            il.Append(incrementBlock);
            il.Append(il.Create(OpCodes.Ldloc_S, tempInt));
            il.Append(OpCodes.Ldc_I4_1);
            il.Append(OpCodes.Add);
            il.Append(il.Create(OpCodes.Stloc_S, tempInt));

            //The comparison block
            il.Append(loopCondition);
            il.Append(il.Create(OpCodes.Ldloc_S, tempAssemblyArray));
            il.Append(OpCodes.Ldlen);
            il.Append(OpCodes.Conv_I4);
            il.Append(OpCodes.Clt);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            il.Append(il.Create(OpCodes.Brtrue_S, startLoopBody));
            //Not found block
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Stloc_2);
            il.Append(il.Create(OpCodes.Br_S, returnBlock));
            il.Append(returnBlock);
            il.Append(OpCodes.Ldloc_2);
            il.Append(OpCodes.Ret);

            //Finally do the try/finally
            ExceptionHandler finallyBlock = new ExceptionHandler(ExceptionHandlerType.Finally); //the lock
            finallyBlock.TryStart = tryStart;
            finallyBlock.TryEnd = finallyStart;
            finallyBlock.HandlerStart = finallyStart;
            finallyBlock.HandlerEnd = afterFinally;
            method.Body.ExceptionHandlers.Add(finallyBlock);
            
            //Return the method
            return method;
        }

        /// <summary>
        /// Builds the start method.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The assembly.</param>
        /// <param name="executingAssembly">The executing assembly.</param>
        /// <param name="assembliesLoaded">The assemblies loaded.</param>
        /// <param name="loadType">Type of the load.</param>
        /// <param name="loadAssemblies">The load assemblies.</param>
        /// <returns></returns>
        private static MethodDefinition BuildStartMethod(ICloakContext context, AssemblyDefinition assembly, FieldReference executingAssembly, FieldReference assembliesLoaded, MethodReference loadType, MethodReference loadAssemblies)
        {
#if USE_FRIENDLY_NAMING
            MethodDefinition method = new MethodDefinition("Start",
                       MethodAttributes.Public | MethodAttributes.HideBySig, assembly.Import(typeof(void)));
#else
            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                   MethodAttributes.Public | MethodAttributes.HideBySig, assembly.Import(typeof(void)));
#endif

            method.Body.InitLocals = true;
            method.Body.MaxStack = 3;
            method.AddLocal(assembly, typeof(string));   //entryAssemblyResource
            method.AddLocal(assembly, typeof(string));   //entryType
            method.AddLocal(assembly, typeof(string));   //entryMethod
            method.AddLocal(assembly, typeof(string));   //resourceName
            var s = method.AddLocal(assembly, typeof(Stream));   //s
            var sr = method.AddLocal(assembly, typeof(StreamReader));   //sr
            var type = method.AddLocal(assembly, typeof(Type));   //type
            var methodInfo = method.AddLocal(assembly, typeof(MethodInfo));   //method
            var tempStringArray = method.AddLocal(assembly, typeof (string[]));
            var tempInt = method.AddLocal(assembly, typeof(int));
            var tempBool = method.AddLocal(assembly, typeof(bool));

            //Get the il builder
            var il = method.Body.CilWorker;

            //Inject the anti reflector code
            InjectAntiReflectorCode(il, il.Create(OpCodes.Nop));

            //Start it off - declare some variables
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Stloc_0);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Stloc_1);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Stloc_2);
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldarg_0);

            //We need to find the entry point so start a loop to find
            il.Append(il.Create(OpCodes.Ldfld, executingAssembly));
            var getManifestResourceNames = assembly.Import(typeof (Assembly).GetMethod("GetManifestResourceNames"));
            il.Append(il.Create(OpCodes.Callvirt, getManifestResourceNames));
            il.Append(il.Create(OpCodes.Stloc_S, tempStringArray));

            //Init the loop counter
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(il.Create(OpCodes.Stloc_S, tempInt));
            //Jump to the loop comparison
            var startLoopComparison = il.Create(OpCodes.Ldloc_S, tempInt);
            il.Append(il.Create(OpCodes.Br, startLoopComparison));

            //Load the current item in a temp variable
            var getTempVar = il.Create(OpCodes.Ldloc_S, tempStringArray);
            il.Append(getTempVar);
            il.Append(il.Create(OpCodes.Ldloc_S, tempInt));
            il.Append(OpCodes.Ldelem_Ref);
            il.Append(OpCodes.Stloc_3);

            //Now start the actual body
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldloc_3);
            il.Append(il.Create(OpCodes.Ldstr, ".e"));
            var endsWith = assembly.Import(typeof (String).GetMethod("EndsWith", new[] {typeof (string)}));
            il.Append(il.Create(OpCodes.Callvirt, endsWith));
            il.Append(OpCodes.Ldc_I4_0); 
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));

            //Branch to increment statement if not true (i.e. equal to 0/false)
            var startIncrement = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Brtrue, startIncrement));
            //Get the stream
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Ldfld, executingAssembly));
            il.Append(OpCodes.Ldloc_3);
            var getManifestResourceStream =
                assembly.Import(typeof (Assembly).GetMethod("GetManifestResourceStream", new[] {typeof (string)}));
            il.Append(il.Create(OpCodes.Callvirt, getManifestResourceStream));
            il.Append(il.Create(OpCodes.Stloc_S, s));
            //Make sure it's not null
            il.Append(il.Create(OpCodes.Ldloc_S, s));
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            //Branch to read it if necessary
            var startUnpack = il.Create(OpCodes.Ldloc_S, s);
            il.Append(il.Create(OpCodes.Brtrue_S, startUnpack));
            var startIncrement2 = il.Create(OpCodes.Ldloc_S, tempInt);
            il.Append(il.Create(OpCodes.Br_S, startIncrement2));
            il.Append(startUnpack);
            //It's all in unicode
            var unicode = assembly.Import(typeof (Encoding).GetProperty("Unicode").GetGetMethod());
            il.Append(il.Create(OpCodes.Call, unicode));
            //Create a new stream reader to read it
            var streamReaderCtor =
                assembly.Import(typeof (StreamReader).GetConstructor(new[] {typeof (Stream), typeof (Encoding)}));
            il.Append(il.Create(OpCodes.Newobj, streamReaderCtor));
            il.Append(il.Create(OpCodes.Stloc_S, sr));

            //Try start
            var tryStart = il.Create(OpCodes.Nop);
            il.Append(tryStart);

            //Load the current bootstrapper namespace
            //il.Append(il.Create(OpCodes.Ldstr, resourceNamespace + "."));
            il.Append(il.Create(OpCodes.Ldloc_S, sr));
            //Read in a line or two
            var readLine = assembly.Import(typeof (TextReader).GetMethod("ReadLine"));
            il.Append(il.Create(OpCodes.Callvirt, readLine));
            //Joing the strings
            //var stringConcat =
            //    assembly.Import(typeof (String).GetMethod("Concat", new[] {typeof (string), typeof (string)}));
            //il.Append(il.Create(OpCodes.Call, stringConcat));
            il.Append(OpCodes.Stloc_0);
            il.Append(il.Create(OpCodes.Ldloc_S, sr));
            il.Append(il.Create(OpCodes.Callvirt, readLine));
            il.Append(OpCodes.Stloc_1);
            il.Append(il.Create(OpCodes.Ldloc_S, sr));
            il.Append(il.Create(OpCodes.Callvirt, readLine));
            il.Append(OpCodes.Stloc_2);

            //HACK - load all assemblies here
            il.Append(OpCodes.Ldarg_0);
            il.Append(OpCodes.Ldc_I4_1);
            il.Append(il.Create(OpCodes.Stfld, assembliesLoaded));
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Call, loadAssemblies));
            //END HACK

            il.Append(OpCodes.Nop);

            var afterFinally = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Leave_S, afterFinally));
            var tryEnd = il.Create(OpCodes.Ldloc_S, sr);
            il.Append(tryEnd);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            var endFinally = il.Create(OpCodes.Endfinally);
            il.Append(il.Create(OpCodes.Brtrue_S, endFinally));
            il.Append(il.Create(OpCodes.Ldloc_S, sr));
            //dispose it
            var dispose = assembly.Import(typeof (IDisposable).GetMethod("Dispose"));
            il.Append(il.Create(OpCodes.Callvirt, dispose));
            il.Append(OpCodes.Nop);
            il.Append(endFinally);
            il.Append(afterFinally);

            //Make sure nothing is null
            il.Append(OpCodes.Ldloc_0);
            var isNullOrEmpty = assembly.Import(typeof (string).GetMethod("IsNullOrEmpty", new[] {typeof (string)}));
            il.Append(il.Create(OpCodes.Call, isNullOrEmpty));
            var exitCheck = il.Create(OpCodes.Ldc_I4_1);
            il.Append(il.Create(OpCodes.Brtrue_S, exitCheck));
            il.Append(OpCodes.Ldloc_1);
            il.Append(il.Create(OpCodes.Call, isNullOrEmpty));
            il.Append(il.Create(OpCodes.Brtrue_S, exitCheck));
            il.Append(OpCodes.Ldloc_2);
            il.Append(il.Create(OpCodes.Call, isNullOrEmpty));
            var doCheck = il.Create(OpCodes.Stloc_S, tempBool);
            il.Append(il.Create(OpCodes.Br_S, doCheck));
            il.Append(exitCheck);
            il.Append(doCheck);
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            var startIncrement3 = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Brtrue_S, startIncrement3));

            //Jump to check if the entry exists
            var checkEntryExists = il.Create(OpCodes.Ldloc_0);
            il.Append(il.Create(OpCodes.Br_S, checkEntryExists));
            il.Append(startIncrement3);
            il.Append(startIncrement);
            il.Append(startIncrement2);
            il.Append(OpCodes.Ldc_I4_1);
            il.Append(OpCodes.Add);
            il.Append(il.Create(OpCodes.Stloc_S, tempInt));

            //Comparison
            il.Append(startLoopComparison);
            il.Append(il.Create(OpCodes.Ldloc_S, tempStringArray));
            il.Append(OpCodes.Ldlen);
            il.Append(OpCodes.Conv_I4);
            il.Append(OpCodes.Clt);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            il.Append(il.Create(OpCodes.Brtrue, getTempVar));

            //Check the entry exists to start the program
            il.Append(checkEntryExists);
            il.Append(il.Create(OpCodes.Call, isNullOrEmpty));
            var failedProgram = il.Create(OpCodes.Ldc_I4_0);
            il.Append(il.Create(OpCodes.Brtrue_S, failedProgram));
            il.Append(OpCodes.Ldloc_1);
            il.Append(il.Create(OpCodes.Call, isNullOrEmpty));
            il.Append(il.Create(OpCodes.Brtrue_S, failedProgram));
            il.Append(OpCodes.Ldloc_2);
            il.Append(il.Create(OpCodes.Call, isNullOrEmpty));
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            var checkIfShouldStart = il.Create(OpCodes.Stloc_S, tempBool);
            il.Append(il.Create(OpCodes.Br_S, checkIfShouldStart));
            il.Append(failedProgram);
            il.Append(checkIfShouldStart);

            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            var startLoad = il.Create(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Brtrue_S, startLoad));
            var ret = il.Create(OpCodes.Ret);
            il.Append(il.Create(OpCodes.Br_S, ret));

            //Start the load type
            il.Append(startLoad);
            il.Append(OpCodes.Ldloc_0);
            il.Append(OpCodes.Ldloc_1);
            il.Append(il.Create(OpCodes.Call, loadType));
            il.Append(il.Create(OpCodes.Stloc_S, type));

#if VERBOSE_OUTPUT
            DebugLine(assembly, il, "Loading type: ", il.Create(OpCodes.Ldloc_0), il.Create(OpCodes.Ldloc_1));
#endif
            //Check it was found
            il.Append(il.Create(OpCodes.Ldloc_S, type));
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            var startFindMethod = il.Create(OpCodes.Ldloc_S, type);
            il.Append(il.Create(OpCodes.Brtrue_S, startFindMethod));
            il.Append(il.Create(OpCodes.Br_S, ret));
            il.Append(startFindMethod);
            il.Append(OpCodes.Ldloc_2);
            il.Append(il.Create(OpCodes.Ldc_I4_S, (sbyte)0x38));
            var getMethod =
                assembly.Import(typeof (Type).GetMethod("GetMethod", new[] {typeof (string), typeof (BindingFlags)}));
            il.Append(il.Create(OpCodes.Callvirt, getMethod));
            il.Append(il.Create(OpCodes.Stloc_S, methodInfo));
#if VERBOSE_OUTPUT
            DebugLine(assembly, il, "Get Method has been called");
#endif

            
            //Check it isn't null
            il.Append(il.Create(OpCodes.Ldloc_S, methodInfo));
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ceq);
            il.Append(OpCodes.Ldc_I4_0);
            il.Append(OpCodes.Ceq);
            il.Append(il.Create(OpCodes.Stloc_S, tempBool));
            il.Append(il.Create(OpCodes.Ldloc_S, tempBool));
            var invokeMethodBlock = il.Create(OpCodes.Ldloc_S, methodInfo);
            il.Append(il.Create(OpCodes.Brtrue_S, invokeMethodBlock));
            il.Append(il.Create(OpCodes.Br_S, ret));
            
            //Invoke the method
            il.Append(invokeMethodBlock);
            il.Append(OpCodes.Ldnull);
            il.Append(OpCodes.Ldnull);
            var invoke =
                assembly.Import(typeof (MethodBase).GetMethod("Invoke", new[] {typeof (object), typeof (object[])}));
            il.Append(il.Create(OpCodes.Callvirt, invoke));
            il.Append(OpCodes.Pop);
#if VERBOSE_OUTPUT
            DebugLine(assembly, il, "Invoke called");
#endif
            il.Append(ret);

            //Try/Finally
            ExceptionHandler finallyBlock = new ExceptionHandler(ExceptionHandlerType.Finally);
            finallyBlock.TryStart = tryStart;
            finallyBlock.TryEnd = tryEnd;
            finallyBlock.HandlerStart = tryEnd;
            finallyBlock.HandlerEnd = afterFinally;
            method.Body.ExceptionHandlers.Add(finallyBlock);
            return method;
        }

#if VERBOSE_OUTPUT
        private static void DebugLine(AssemblyDefinition assembly, CilWorker il, string text, params Instruction[] appendInstructions)
        {
            il.Append(il.Create(OpCodes.Ldstr, text));
            //Append any instructions we need to
            if (appendInstructions.Length > 0)
            {
                var concat =
                    assembly.Import(typeof (String).GetMethod("Concat", new[] {typeof (string), typeof (string)}));
                foreach (Instruction i in appendInstructions)
                {
                    il.Append(il.Create(OpCodes.Ldstr, ","));
                    il.Append(il.Create(OpCodes.Call, concat));
                    il.Append(i);
                    il.Append(il.Create(OpCodes.Call, concat));
                }
            }
            var writeLine = assembly.Import(typeof (Console).GetMethod("WriteLine", new[] {typeof (string)}));
            il.Append(il.Create(OpCodes.Call, writeLine));
        }
#endif

        /// <summary>
        /// Builds the program constructor.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="programType">Type of the program.</param>
        /// <param name="assemblyLock">The assembly lock.</param>
        /// <param name="executingAssembly">The executing assembly.</param>
        /// <param name="loadedAssemblies">The loaded assemblies.</param>
        /// <param name="currentDomain">The current domain.</param>
        /// <param name="eventHandler">The event handler.</param>
        /// <param name="assemblyResolve">The assembly resolve.</param>
        /// <param name="getExecutingAssembly">The get executing assembly.</param>
        /// <param name="resolveMethod">The resolve method.</param>
        private static void BuildProgramConstructor(AssemblyDefinition assembly, TypeDefinition programType, FieldReference assemblyLock, FieldReference executingAssembly, FieldReference loadedAssemblies, MethodReference currentDomain, MethodReference eventHandler, MethodReference assemblyResolve, MethodReference getExecutingAssembly, MethodReference resolveMethod)
        {
            var body = assembly.CreateDefaultConstructor(programType);
            body.MaxStack = 4;
            body.InitLocals = true;
            body.Variables.Add(new VariableDefinition(assembly.Import(typeof(AppDomain))));
            var il = body.CilWorker;

            //Inject anti reflector code
            InjectAntiReflectorCode(il, il.Create(OpCodes.Ldarg_0));

            //define the assembly lock
            var objConstructor = assembly.Import(typeof(object).GetConstructor(Type.EmptyTypes));
            il.Append(il.Create(OpCodes.Newobj, objConstructor));
            il.Append(il.Create(OpCodes.Stfld, assemblyLock));
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Call, objConstructor));
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldarg_0);
            var cacheConstructor = assembly.Import(typeof(Dictionary<string, Assembly>).GetConstructor(Type.EmptyTypes));
            il.Append(il.Create(OpCodes.Newobj, cacheConstructor));
            il.Append(il.Create(OpCodes.Stfld, loadedAssemblies));
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Call, getExecutingAssembly));
            il.Append(il.Create(OpCodes.Stfld, executingAssembly));
            il.Append(il.Create(OpCodes.Call, currentDomain));
            il.Append(OpCodes.Stloc_0);
            il.Append(OpCodes.Ldloc_0);
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Ldftn, resolveMethod));
            il.Append(il.Create(OpCodes.Newobj, eventHandler));
            il.Append(il.Create(OpCodes.Callvirt, assemblyResolve));
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ret);
        }

        /// <summary>
        /// Builds the main method.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="context">The context.</param>
        /// <param name="programType">Type of the program.</param>
        /// <param name="getExecutingAssembly">The get executing assembly.</param>
        /// <param name="currentDomain">The current domain.</param>
        /// <param name="eventHandler">The event handler.</param>
        /// <param name="assemblyResolve">The assembly resolve.</param>
        /// <param name="resolveMethod">The resolve method.</param>
        /// <param name="startMethod">The start method.</param>
        /// <returns></returns>
        private static MethodDefinition BuildMainMethod(AssemblyDefinition assembly, ICloakContext context, TypeDefinition programType, MethodReference getExecutingAssembly, MethodReference currentDomain, MethodReference eventHandler, MethodReference assemblyResolve, MethodReference resolveMethod, MethodReference startMethod)
        {
#if USE_FRIENDLY_NAMING
            MethodDefinition entryPoint =
                new MethodDefinition("Main",
                                     MethodAttributes.Private | MethodAttributes.Static |
                                     MethodAttributes.HideBySig, assembly.Import(typeof(void)));
#else
            MethodDefinition entryPoint =
                new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                     MethodAttributes.Private | MethodAttributes.Static |
                                     MethodAttributes.HideBySig, assembly.Import(typeof(void)));
#endif
            entryPoint.Body.InitLocals = true;
            entryPoint.Body.MaxStack = 4;

#if USE_APPDOMAIN
            //Initialise some locals
            entryPoint.AddLocal(assembly, typeof(AppDomain));
            entryPoint.AddLocal(assembly, typeof(Assembly));
#endif
            VariableDefinition result = new VariableDefinition(programType);
            entryPoint.Body.Variables.Add(result);

            //Add the method
            assembly.EntryPoint = entryPoint;

            //Declare the il to build the code
            CilWorker il = entryPoint.Body.CilWorker;

            //First of all add the anti reflector code
            InjectAntiReflectorCode(il, il.Create(OpCodes.Nop));

            //Now output the code - essentially:
            /*
            AppDomain domain = AppDomain.CreateDomain("App");
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            ProgramRunner runner = (ProgramRunner)domain.CreateInstanceAndUnwrap(executingAssembly.FullName, "TestBootstrapper.ProgramRunner");
            AppDomain.CurrentDomain.AssemblyResolve += runner.ResolveAssembly;
            runner.Start();
             */
#if USE_APPDOMAIN
#if USE_FRIENDLY_NAMING
            il.Append(il.Create(OpCodes.Ldstr, "AppDomainName"));
#else
            il.Append(il.Create(OpCodes.Ldstr, context.NameManager.GenerateName(NamingTable.Type)));
#endif

            //Get the AppDomain::Create(string) method
            var appDomainCreate = assembly.Import(typeof(AppDomain).GetMethod("CreateDomain", new[] { typeof(string) }));
            il.Append(il.Create(OpCodes.Call, appDomainCreate));
            il.Append(OpCodes.Stloc_0);

            //Get the Assembly::GetExecutingAssembly() method
            il.Append(il.Create(OpCodes.Call, getExecutingAssembly));

            il.Append(OpCodes.Stloc_1);
            il.Append(OpCodes.Ldloc_0);
            il.Append(OpCodes.Ldloc_1);

            //Assembly::get_FullName()
            var getFullName = typeof(Assembly).GetProperty("FullName");
            il.Append(il.Create(OpCodes.Callvirt, assembly.Import(getFullName.GetGetMethod())));

            il.Append(il.Create(OpCodes.Ldstr,
                                String.Format("{0}.{1}", programType.Namespace, programType.Name)));

            //AppDomain::CreateInstanceAndUnwrap(string, string)
            var createInstanceAndUnwrap = typeof(AppDomain).GetMethod("CreateInstanceAndUnwrap",
                                                                       new[]
                                                                                       {
                                                                                           typeof (string), typeof (string)
                                                                                       });
            il.Append(il.Create(OpCodes.Callvirt, assembly.Import(createInstanceAndUnwrap)));
            il.Append(il.Create(OpCodes.Castclass, programType));
            il.Append(OpCodes.Stloc_2);

            //AppDomain::get_CurrentDomain
            il.Append(il.Create(OpCodes.Call, currentDomain));
            il.Append(OpCodes.Ldloc_2);

            //Get the function
            il.Append(il.Create(OpCodes.Ldftn, resolveMethod));
            il.Append(il.Create(OpCodes.Newobj, eventHandler));
            il.Append(il.Create(OpCodes.Callvirt, assemblyResolve));

            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldloc_2);
#else
            il.Append(il.Create(OpCodes.Newobj, programType.Constructors[0]));
            il.Append(OpCodes.Stloc_0);
            il.Append(OpCodes.Ldloc_0);
#endif
            //Start the program
            il.Append(il.Create(OpCodes.Callvirt, startMethod));
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ret);
            return entryPoint;
        }

        /// <summary>
        /// Injects the anti reflector code.
        /// </summary>
        /// <param name="il">The il builder.</param>
        /// <param name="first">The first instruction to use.</param>
        private static void InjectAntiReflectorCode(CilWorker il, Instruction first)
        {
            il.Append(il.Create(OpCodes.Br_S, first));
            il.Append(ConfuseDecompilationTask.CreateInvalidOpCode());
            il.Append(ConfuseDecompilationTask.CreateInvalidOpCode());
            il.Append(first);
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
