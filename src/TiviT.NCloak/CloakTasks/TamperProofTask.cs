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

            //Now make it do something
            BuildBootstrapper(context, bootstrapperAssembly);

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
        private void BuildBootstrapper(ICloakContext context, AssemblyDefinition assembly)
        {
            //See http://blog.paul-mason.co.nz/2010/02/tamper-proofing-implementation-part-2.html

            //Declare some types
            var boolType = assembly.Import(typeof(bool));
            var objType = assembly.Import(typeof(object));
            var voidType = assembly.Import(typeof(void));
            var cacheType = assembly.Import(typeof(Dictionary<string, Assembly>));
            var byteArrayType = assembly.Import(typeof(byte[]));
            var stringType = assembly.Import(typeof(string));

            //First create the actual program runner
            TypeDefinition programType = new TypeDefinition(context.NameManager.GenerateName(NamingTable.Type),
                                                        context.NameManager.GenerateName(NamingTable.Type),
                                                        TypeAttributes.NotPublic | TypeAttributes.Class |
                                                        TypeAttributes.AutoClass | TypeAttributes.AnsiClass |
                                                        TypeAttributes.Serializable | TypeAttributes.BeforeFieldInit,
                                                        objType);
            assembly.MainModule.Types.Add(programType);

            //Add the class level fields
            string assembliesLoadedVariableName = context.NameManager.GenerateName(NamingTable.Field);
            string assemblyLockVariableName = context.NameManager.GenerateName(NamingTable.Field);
            string executingAssemblyVariableName = context.NameManager.GenerateName(NamingTable.Field);
            string loadedAssembliesVariableName = context.NameManager.GenerateName(NamingTable.Field);
            var assembliesLoaded = new FieldDefinition(assembliesLoadedVariableName, boolType, FieldAttributes.Private);
            var assemblyLock = new FieldDefinition(assemblyLockVariableName, objType, FieldAttributes.Private | FieldAttributes.InitOnly);
            var executingAssembly = new FieldDefinition(executingAssemblyVariableName, assembly.Import(typeof(Assembly)),
                                                        FieldAttributes.Private | FieldAttributes.InitOnly);
            var loadedAssemblies = new FieldDefinition(loadedAssembliesVariableName,
                                                       cacheType,
                                                       FieldAttributes.Private | FieldAttributes.InitOnly);
            programType.Fields.Add(assembliesLoaded);
            programType.Fields.Add(assemblyLock);
            programType.Fields.Add(executingAssembly);
            programType.Fields.Add(loadedAssemblies);

            //Get some method references we share
            MethodReference currentDomain = assembly.Import(typeof(AppDomain).GetProperty("CurrentDomain").GetGetMethod());
            MethodReference eventHandler = assembly.Import(typeof(ResolveEventHandler).GetConstructor(new[] { typeof(object), typeof(int) }));
            MethodReference assemblyResolve = assembly.Import(typeof(AppDomain).GetEvent("AssemblyResolve").GetAddMethod());
            MethodReference getExecutingAssembly = assembly.Import(typeof(Assembly).GetMethod("GetExecutingAssembly"));

            //Define decrypt data method
            var decryptMethod = BuildDecryptMethod(context, assembly, byteArrayType);
            programType.Methods.Add(decryptMethod);

            //Define hash data method
            var hashMethod = BuildHashMethod(context, assembly, stringType);
            programType.Methods.Add(hashMethod);

            //Define load assembly
            var loadAssemblyMethod = BuildLoadAssemblyMethod(context, assembly, stringType, loadedAssemblies);
            programType.Methods.Add(loadAssemblyMethod);

            //Define load type method
            var loadTypeMethod = BuildLoadTypeMethod(context, assembly, stringType, loadAssemblyMethod);
            programType.Methods.Add(loadTypeMethod);

            //Define load assemblies method
            var loadAssembliesMethod = BuildLoadAssembliesMethod(context, assembly, voidType, executingAssembly, loadAssemblyMethod);
            programType.Methods.Add(loadAssembliesMethod);

            //Define resolve method
            var resolveMethod = BuildResolveMethod(context, assembly);
            programType.Methods.Add(resolveMethod);

            //Define start method
            var startMethod = BuildStartMethod(context, assembly);
            programType.Methods.Add(startMethod);

            //Now define a constructor
            BuildProgramConstructor(assembly, programType, assemblyLock, executingAssembly, loadedAssemblies, currentDomain, eventHandler, assemblyResolve, getExecutingAssembly, resolveMethod);

            //Now create a type to hold the entry point
            TypeDefinition entryType = new TypeDefinition(context.NameManager.GenerateName(NamingTable.Type),
                                                        context.NameManager.GenerateName(NamingTable.Type),
                                                        TypeAttributes.NotPublic | TypeAttributes.Class |
                                                        TypeAttributes.AutoClass | TypeAttributes.AnsiClass |
                                                        TypeAttributes.BeforeFieldInit,
                                                        objType);
            assembly.MainModule.Types.Add(entryType);

            //Create a default constructor
            var ctor = assembly.CreateDefaultConstructor(entryType);
            ctor.MaxStack = 8;
            var il = ctor.CilWorker;
            InjectAntiReflectorCode(il);
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Newobj, objType));
            il.Append(OpCodes.Ret);

            //Create an entry point
            var mainMethod = BuildMainMethod(assembly, context, programType, getExecutingAssembly, currentDomain, eventHandler, assemblyResolve, voidType, resolveMethod, startMethod);
            entryType.Methods.Add(mainMethod);
        }

        /// <summary>
        /// Builds the load assemblies method.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The assembly.</param>
        /// <param name="voidType">Type of the void.</param>
        /// <param name="executingAssembly">The executing assembly.</param>
        /// <param name="loadAssembly">The load assembly.</param>
        /// <returns></returns>
        private MethodDefinition BuildLoadAssembliesMethod(ICloakContext context, AssemblyDefinition assembly, TypeReference voidType, FieldReference executingAssembly, MethodReference loadAssembly)
        {
            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                               MethodAttributes.Private | MethodAttributes.HideBySig, voidType);
            method.Body.InitLocals = true;
            method.Body.MaxStack = 2;
            method.AddLocal(typeof (string)); //Resource name
            method.AddLocal(typeof (string[])); //Foreach temp
            method.AddLocal(typeof (int)); //Loop counter
            method.AddLocal(typeof(bool));

            //Build the body
            var il = method.Body.CilWorker;
            InjectAntiReflectorCode(il);
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldarg_0);
            il.Append(il.Create(OpCodes.Ldfld, executingAssembly));

            //Get the resources - foreach get's converted to a standard loop
            var resourcesMethod = typeof (Assembly).GetMethod("GetManifestResourceNames");
            il.Append(il.Create(OpCodes.Callvirt, assembly.Import(resourcesMethod)));
            il.Append(OpCodes.Stloc_0);

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
        /// <param name="stringType">Type of the string.</param>
        /// <param name="loadedAssemblies">The loaded assemblies.</param>
        /// <returns></returns>
        private MethodDefinition BuildLoadAssemblyMethod(ICloakContext context, AssemblyDefinition assembly, TypeReference stringType, FieldReference loadedAssemblies)
        {
            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                   MethodAttributes.Private | MethodAttributes.HideBySig, assembly.Import(typeof(Assembly)));

            //Declare the resource parameter
            method.Parameters.Add(new ParameterDefinition(stringType));

            method.Body.InitLocals = true;
            method.Body.MaxStack = 4;
            method.AddLocal(typeof(string)); //Hash
            method.AddLocal(typeof(Stream)); //Stream for loading
            method.AddLocal(typeof(byte[])); //Hash data
            method.AddLocal(typeof(byte[])); //Data
            var password = method.AddLocal(typeof(Rfc2898DeriveBytes)); //Password
            var keyBytes = method.AddLocal(typeof(byte[])); //Key bytes
            var initVector = method.AddLocal(typeof(byte[])); //Init vector
            var rawAssembly = method.AddLocal(typeof(byte[])); //Assembly raw bytes
            var actualAssembly = method.AddLocal(typeof(Assembly)); //Actual Assembly
            var tempAssembly = method.AddLocal(typeof(Assembly)); //Temp Assembly
            var tempBool = method.AddLocal(typeof(bool)); //Temp bool

            //Build the body
            var il = method.Body.CilWorker;
            InjectAntiReflectorCode(il);

            //Start the code
            il.Append(OpCodes.Nop);
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
//    L_0014: brtrue.s L_0029
//    L_0016: ldarg.0 
//    L_0017: ldfld class [mscorlib]System.Collections.Generic.Dictionary`2<string, class [mscorlib]System.Reflection.Assembly> TestBootstrapper.ProgramRunner::loadedAssemblies
//    L_001c: ldarg.1 
//    L_001d: callvirt instance !1 [mscorlib]System.Collections.Generic.Dictionary`2<string, class [mscorlib]System.Reflection.Assembly>::get_Item(!0)
//    L_0022: stloc.s CS$1$0000
//    L_0024: br L_0163
//    L_0029: ldarg.0 
//    L_002a: ldfld class [mscorlib]System.Reflection.Assembly TestBootstrapper.ProgramRunner::executingAssembly
//    L_002f: ldarg.1 
//    L_0030: ldstr ".v0"
//    L_0035: call string [mscorlib]System.String::Concat(string, string)
//    L_003a: callvirt instance class [mscorlib]System.IO.Stream [mscorlib]System.Reflection.Assembly::GetManifestResourceStream(string)
//    L_003f: stloc.1 
//    L_0040: nop 
//    L_0041: ldloc.1 
//    L_0042: ldnull 
//    L_0043: ceq 
//    L_0045: ldc.i4.0 
//    L_0046: ceq 
//    L_0048: stloc.s CS$4$0001
//    L_004a: ldloc.s CS$4$0001
//    L_004c: brtrue.s L_0056
//    L_004e: ldnull 
//    L_004f: stloc.s CS$1$0000
//    L_0051: leave L_0163
//    L_0056: ldloc.1 
//    L_0057: callvirt instance int64 [mscorlib]System.IO.Stream::get_Length()
//    L_005c: conv.ovf.i 
//    L_005d: newarr uint8
//    L_0062: stloc.2 
//    L_0063: ldloc.1 
//    L_0064: ldloc.2 
//    L_0065: ldc.i4.0 
//    L_0066: ldloc.2 
//    L_0067: ldlen 
//    L_0068: conv.i4 
//    L_0069: callvirt instance int32 [mscorlib]System.IO.Stream::Read(uint8[], int32, int32)
//    L_006e: pop 
//    L_006f: ldloc.2 
//    L_0070: call string [mscorlib]System.Convert::ToBase64String(uint8[])
//    L_0075: stloc.0 
//    L_0076: nop 
//    L_0077: leave.s L_008b
//    L_0079: ldloc.1 
//    L_007a: ldnull 
//    L_007b: ceq 
//    L_007d: stloc.s CS$4$0001
//    L_007f: ldloc.s CS$4$0001
//    L_0081: brtrue.s L_008a
//    L_0083: ldloc.1 
//    L_0084: callvirt instance void [mscorlib]System.IDisposable::Dispose()
//    L_0089: nop 
//    L_008a: endfinally 
//    L_008b: nop 
//    L_008c: ldarg.0 
//    L_008d: ldfld class [mscorlib]System.Reflection.Assembly TestBootstrapper.ProgramRunner::executingAssembly
//    L_0092: ldarg.1 
//    L_0093: callvirt instance class [mscorlib]System.IO.Stream [mscorlib]System.Reflection.Assembly::GetManifestResourceStream(string)
//    L_0098: stloc.1 
//    L_0099: nop 
//    L_009a: ldloc.1 
//    L_009b: ldnull 
//    L_009c: ceq 
//    L_009e: ldc.i4.0 
//    L_009f: ceq 
//    L_00a1: stloc.s CS$4$0001
//    L_00a3: ldloc.s CS$4$0001
//    L_00a5: brtrue.s L_00af
//    L_00a7: ldnull 
//    L_00a8: stloc.s CS$1$0000
//    L_00aa: leave L_0163
//    L_00af: ldloc.1 
//    L_00b0: callvirt instance int64 [mscorlib]System.IO.Stream::get_Length()
//    L_00b5: conv.ovf.i 
//    L_00b6: newarr uint8
//    L_00bb: stloc.3 
//    L_00bc: ldloc.1 
//    L_00bd: ldloc.3 
//    L_00be: ldc.i4.0 
//    L_00bf: ldloc.3 
//    L_00c0: ldlen 
//    L_00c1: conv.i4 
//    L_00c2: callvirt instance int32 [mscorlib]System.IO.Stream::Read(uint8[], int32, int32)
//    L_00c7: pop 
//    L_00c8: nop 
//    L_00c9: leave.s L_00dd
//    L_00cb: ldloc.1 
//    L_00cc: ldnull 
//    L_00cd: ceq 
//    L_00cf: stloc.s CS$4$0001
//    L_00d1: ldloc.s CS$4$0001
//    L_00d3: brtrue.s L_00dc
//    L_00d5: ldloc.1 
//    L_00d6: callvirt instance void [mscorlib]System.IDisposable::Dispose()
//    L_00db: nop 
//    L_00dc: endfinally 
//    L_00dd: nop 
//    L_00de: ldstr "ee7ad9b5-1462-47f6-849e-37190a7751ee"
//    L_00e3: ldstr "d3rImlxQskaHWUy80gSCjg=="
//    L_00e8: call uint8[] [mscorlib]System.Convert::FromBase64String(string)
//    L_00ed: ldc.i4.2 
//    L_00ee: newobj instance void [mscorlib]System.Security.Cryptography.Rfc2898DeriveBytes::.ctor(string, uint8[], int32)
//    L_00f3: stloc.s password
//    L_00f5: ldloc.s password
//    L_00f7: ldc.i4.s 0x20
//    L_00f9: callvirt instance uint8[] [mscorlib]System.Security.Cryptography.DeriveBytes::GetBytes(int32)
//    L_00fe: stloc.s keyBytes
//    L_0100: ldloc.s password
//    L_0102: ldc.i4.s 0x10
//    L_0104: callvirt instance uint8[] [mscorlib]System.Security.Cryptography.DeriveBytes::GetBytes(int32)
//    L_0109: stloc.s initVector
//    L_010b: ldloc.3 
//    L_010c: ldloc.s keyBytes
//    L_010e: ldloc.s initVector
//    L_0110: call uint8[] TestBootstrapper.ProgramRunner::DecryptData(uint8[], uint8[], uint8[])
//    L_0115: stloc.s 'assembly'
//    L_0117: ldloc.0 
//    L_0118: ldloc.s 'assembly'
//    L_011a: call string TestBootstrapper.ProgramRunner::HashData(uint8[])
//    L_011f: call bool [mscorlib]System.String::op_Inequality(string, string)
//    L_0124: ldc.i4.0 
//    L_0125: ceq 
//    L_0127: stloc.s CS$4$0001
//    L_0129: ldloc.s CS$4$0001
//    L_012b: brtrue.s L_0132
//    L_012d: ldnull 
//    L_012e: stloc.s CS$1$0000
//    L_0130: br.s L_0163
//    L_0132: ldloc.s 'assembly'
//    L_0134: call class [mscorlib]System.Reflection.Assembly [mscorlib]System.Reflection.Assembly::Load(uint8[])
//    L_0139: stloc.s asm
//    L_013b: ldloc.s asm
//    L_013d: ldnull 
//    L_013e: ceq 
//    L_0140: ldc.i4.0 
//    L_0141: ceq 
//    L_0143: stloc.s CS$4$0001
//    L_0145: ldloc.s CS$4$0001
//    L_0147: brtrue.s L_014e
//    L_0149: ldnull 
//    L_014a: stloc.s CS$1$0000
//    L_014c: br.s L_0163
//    L_014e: ldarg.0 
//    L_014f: ldfld class [mscorlib]System.Collections.Generic.Dictionary`2<string, class [mscorlib]System.Reflection.Assembly> TestBootstrapper.ProgramRunner::loadedAssemblies
//    L_0154: ldarg.1 
//    L_0155: ldloc.s asm
//    L_0157: callvirt instance void [mscorlib]System.Collections.Generic.Dictionary`2<string, class [mscorlib]System.Reflection.Assembly>::Add(!0, !1)
//    L_015c: nop 
//    L_015d: ldloc.s asm
//    L_015f: stloc.s CS$1$0000
//    L_0161: br.s L_0163
//    L_0163: nop 
//    L_0164: ldloc.s CS$1$0000
//    L_0166: ret 
//    .try L_0040 to L_0079 finally handler L_0079 to L_008b
//    .try L_0099 to L_00cb finally handler L_00cb to L_00dd
//}

            return method;
        }

        /// <summary>
        /// Builds the load type method.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The assembly.</param>
        /// <param name="stringType">Type of the string.</param>
        /// <param name="loadAssembly">The load assembly.</param>
        /// <returns></returns>
        private MethodDefinition BuildLoadTypeMethod(ICloakContext context, AssemblyDefinition assembly, TypeReference stringType, MethodReference loadAssembly)
        {
            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                   MethodAttributes.Private | MethodAttributes.HideBySig, assembly.Import(typeof(Type)));

            //Declare the resource parameter
            method.Parameters.Add(new ParameterDefinition(stringType));
            method.Parameters.Add(new ParameterDefinition(stringType));
            
            method.Body.InitLocals = true;
            method.Body.MaxStack = 2;
            method.AddLocal(typeof(Assembly)); //Assembly
            method.AddLocal(typeof(Type)); //Temp variable for return type
            method.AddLocal(typeof(bool)); //Temp variable for comparison

            //Start with injection
            var il = method.Body.CilWorker;
            InjectAntiReflectorCode(il);

            //Start the code
            il.Append(OpCodes.Nop);
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
        /// <param name="stringType">Type of the string.</param>
        /// <returns></returns>
        private MethodDefinition BuildHashMethod(ICloakContext context, AssemblyDefinition assembly, TypeReference stringType)
        {

            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                               MethodAttributes.Private | MethodAttributes.HideBySig |
                                               MethodAttributes.Static, stringType);
            method.Body.InitLocals = true;
            method.Body.MaxStack = 2;
            method.AddLocal(typeof(SHA256));
            method.AddLocal(typeof(string));

            //Easy method to output
            var il = method.Body.CilWorker;

            //Inject some anti reflector stuff
            InjectAntiReflectorCode(il);
            il.Append(OpCodes.Nop);

            //Create the SHA256 object
            var sha256Create = typeof(SHA256).GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
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
        /// <param name="byteArrayType">Type of the byte array.</param>
        /// <returns></returns>
        private MethodDefinition BuildDecryptMethod(ICloakContext context, AssemblyDefinition assembly, TypeReference byteArrayType)
        {
            MethodDefinition method = new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                                           MethodAttributes.Private | MethodAttributes.HideBySig |
                                                           MethodAttributes.Static, byteArrayType);
            method.Body.InitLocals = true;
            method.Body.MaxStack = 5;

            //Declare the locals - first four have quick reference
            method.AddLocal(typeof(Rijndael));
            method.AddLocal(typeof(ICryptoTransform));
            method.AddLocal(typeof(MemoryStream));
            method.AddLocal(typeof(CryptoStream));
            var paddedPlain = method.AddLocal(typeof(byte[]));
            var length = method.AddLocal(typeof(int));
            var plain = method.AddLocal(typeof(byte[]));
            var returnArray = method.AddLocal(typeof(byte[]));
            var inferredBool = method.AddLocal(typeof(bool));

            //Add the body
            var il = method.Body.CilWorker;

            //Inject anti reflector code
            InjectAntiReflectorCode(il);

            //Start the body
            il.Append(OpCodes.Nop);
            var rijndaelCreate = typeof(Rijndael).GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
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
        /// <param name="definition">The definition.</param>
        /// <returns></returns>
        private MethodDefinition BuildResolveMethod(ICloakContext context, AssemblyDefinition definition)
        {
//.method public hidebysig instance class [mscorlib]System.Reflection.Assembly ResolveAssembly(object sender, class [mscorlib]System.ResolveEventArgs args) cil managed
//{
//    .maxstack 2
//    .locals init (
//        [0] class [mscorlib]System.Reflection.Assembly[] currentAssemblies,
//        [1] class [mscorlib]System.Reflection.Assembly a,
//        [2] class [mscorlib]System.Reflection.Assembly CS$1$0000,
//        [3] object CS$2$0001,
//        [4] bool CS$4$0002,
//        [5] class [mscorlib]System.Reflection.Assembly[] CS$6$0003,
//        [6] int32 CS$7$0004)
//    L_0000: nop 
//    L_0001: ldarg.0 
//    L_0002: ldfld object TestBootstrapper.ProgramRunner::assemblyLock
//    L_0007: dup 
//    L_0008: stloc.3 
//    L_0009: call void [mscorlib]System.Threading.Monitor::Enter(object)
//    L_000e: nop 
//    L_000f: nop 
//    L_0010: ldarg.0 
//    L_0011: ldfld bool TestBootstrapper.ProgramRunner::assembliesLoaded
//    L_0016: stloc.s CS$4$0002
//    L_0018: ldloc.s CS$4$0002
//    L_001a: brtrue.s L_002c
//    L_001c: nop 
//    L_001d: ldarg.0 
//    L_001e: ldc.i4.1 
//    L_001f: stfld bool TestBootstrapper.ProgramRunner::assembliesLoaded
//    L_0024: ldarg.0 
//    L_0025: call instance void TestBootstrapper.ProgramRunner::LoadAssemblies()
//    L_002a: nop 
//    L_002b: nop 
//    L_002c: nop 
//    L_002d: leave.s L_0037
//    L_002f: ldloc.3 
//    L_0030: call void [mscorlib]System.Threading.Monitor::Exit(object)
//    L_0035: nop 
//    L_0036: endfinally 
//    L_0037: nop 
//    L_0038: call class [mscorlib]System.AppDomain [mscorlib]System.AppDomain::get_CurrentDomain()
//    L_003d: callvirt instance class [mscorlib]System.Reflection.Assembly[] [mscorlib]System.AppDomain::GetAssemblies()
//    L_0042: stloc.0 
//    L_0043: nop 
//    L_0044: ldloc.0 
//    L_0045: stloc.s CS$6$0003
//    L_0047: ldc.i4.0 
//    L_0048: stloc.s CS$7$0004
//    L_004a: br.s L_0078
//    L_004c: ldloc.s CS$6$0003
//    L_004e: ldloc.s CS$7$0004
//    L_0050: ldelem.ref 
//    L_0051: stloc.1 
//    L_0052: nop 
//    L_0053: ldloc.1 
//    L_0054: callvirt instance string [mscorlib]System.Reflection.Assembly::get_FullName()
//    L_0059: ldarg.2 
//    L_005a: callvirt instance string [mscorlib]System.ResolveEventArgs::get_Name()
//    L_005f: call bool [mscorlib]System.String::op_Equality(string, string)
//    L_0064: ldc.i4.0 
//    L_0065: ceq 
//    L_0067: stloc.s CS$4$0002
//    L_0069: ldloc.s CS$4$0002
//    L_006b: brtrue.s L_0071
//    L_006d: ldloc.1 
//    L_006e: stloc.2 
//    L_006f: leave.s L_008a
//    L_0071: nop 
//    L_0072: ldloc.s CS$7$0004
//    L_0074: ldc.i4.1 
//    L_0075: add 
//    L_0076: stloc.s CS$7$0004
//    L_0078: ldloc.s CS$7$0004
//    L_007a: ldloc.s CS$6$0003
//    L_007c: ldlen 
//    L_007d: conv.i4 
//    L_007e: clt 
//    L_0080: stloc.s CS$4$0002
//    L_0082: ldloc.s CS$4$0002
//    L_0084: brtrue.s L_004c
//    L_0086: ldnull 
//    L_0087: stloc.2 
//    L_0088: br.s L_008a
//    L_008a: nop 
//    L_008b: ldloc.2 
//    L_008c: ret 
//    .try L_000f to L_002f finally handler L_002f to L_0037
//}


            throw new NotImplementedException();
        }

        /// <summary>
        /// Builds the start method.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="definition">The definition.</param>
        /// <returns></returns>
        private MethodDefinition BuildStartMethod(ICloakContext context, AssemblyDefinition definition)
        {
            /*
            .method public hidebysig instance void Start() cil managed
            {
                .maxstack 3
                .locals init (
                    [0] string entryAssemblyResource,
                    [1] string entryType,
                    [2] string entryMethod,
                    [3] string resourceName,
                    [4] class [mscorlib]System.IO.Stream s,
                    [5] class [mscorlib]System.IO.StreamReader sr,
                    [6] class [mscorlib]System.Type 'type',
                    [7] class [mscorlib]System.Reflection.MethodInfo 'method',
                    [8] string[] CS$6$0000,
                    [9] int32 CS$7$0001,
                    [10] bool CS$4$0002)
                L_0000: nop 
                L_0001: ldnull 
                L_0002: stloc.0 
                L_0003: ldnull 
                L_0004: stloc.1 
                L_0005: ldnull 
                L_0006: stloc.2 
                L_0007: nop 
                L_0008: ldarg.0 
                L_0009: ldfld class [mscorlib]System.Reflection.Assembly TestBootstrapper.ProgramRunner::executingAssembly
                L_000e: callvirt instance string[] [mscorlib]System.Reflection.Assembly::GetManifestResourceNames()
                L_0013: stloc.s CS$6$0000
                L_0015: ldc.i4.0 
                L_0016: stloc.s CS$7$0001
                L_0018: br L_00cc
                L_001d: ldloc.s CS$6$0000
                L_001f: ldloc.s CS$7$0001
                L_0021: ldelem.ref 
                L_0022: stloc.3 
                L_0023: nop 
                L_0024: ldloc.3 
                L_0025: ldstr ".e"
                L_002a: callvirt instance bool [mscorlib]System.String::EndsWith(string)
                L_002f: ldc.i4.0 
                L_0030: ceq 
                L_0032: stloc.s CS$4$0002
                L_0034: ldloc.s CS$4$0002
                L_0036: brtrue L_00c5
                L_003b: nop 
                L_003c: ldarg.0 
                L_003d: ldfld class [mscorlib]System.Reflection.Assembly TestBootstrapper.ProgramRunner::executingAssembly
                L_0042: ldloc.3 
                L_0043: callvirt instance class [mscorlib]System.IO.Stream [mscorlib]System.Reflection.Assembly::GetManifestResourceStream(string)
                L_0048: stloc.s s
                L_004a: ldloc.s s
                L_004c: ldnull 
                L_004d: ceq 
                L_004f: ldc.i4.0 
                L_0050: ceq 
                L_0052: stloc.s CS$4$0002
                L_0054: ldloc.s CS$4$0002
                L_0056: brtrue.s L_005a
                L_0058: br.s L_00c6
                L_005a: ldloc.s s
                L_005c: call class [mscorlib]System.Text.Encoding [mscorlib]System.Text.Encoding::get_Unicode()
                L_0061: newobj instance void [mscorlib]System.IO.StreamReader::.ctor(class [mscorlib]System.IO.Stream, class [mscorlib]System.Text.Encoding)
                L_0066: stloc.s sr
                L_0068: nop 
                L_0069: ldstr "TestBootstrapper.Resources."
                L_006e: ldloc.s sr
                L_0070: callvirt instance string [mscorlib]System.IO.TextReader::ReadLine()
                L_0075: call string [mscorlib]System.String::Concat(string, string)
                L_007a: stloc.0 
                L_007b: ldloc.s sr
                L_007d: callvirt instance string [mscorlib]System.IO.TextReader::ReadLine()
                L_0082: stloc.1 
                L_0083: ldloc.s sr
                L_0085: callvirt instance string [mscorlib]System.IO.TextReader::ReadLine()
                L_008a: stloc.2 
                L_008b: nop 
                L_008c: leave.s L_00a2
                L_008e: ldloc.s sr
                L_0090: ldnull 
                L_0091: ceq 
                L_0093: stloc.s CS$4$0002
                L_0095: ldloc.s CS$4$0002
                L_0097: brtrue.s L_00a1
                L_0099: ldloc.s sr
                L_009b: callvirt instance void [mscorlib]System.IDisposable::Dispose()
                L_00a0: nop 
                L_00a1: endfinally 
                L_00a2: nop 
                L_00a3: ldloc.0 
                L_00a4: call bool [mscorlib]System.String::IsNullOrEmpty(string)
                L_00a9: brtrue.s L_00bb
                L_00ab: ldloc.1 
                L_00ac: call bool [mscorlib]System.String::IsNullOrEmpty(string)
                L_00b1: brtrue.s L_00bb
                L_00b3: ldloc.2 
                L_00b4: call bool [mscorlib]System.String::IsNullOrEmpty(string)
                L_00b9: br.s L_00bc
                L_00bb: ldc.i4.1 
                L_00bc: stloc.s CS$4$0002
                L_00be: ldloc.s CS$4$0002
                L_00c0: brtrue.s L_00c4
                L_00c2: br.s L_00dd
                L_00c4: nop 
                L_00c5: nop 
                L_00c6: ldloc.s CS$7$0001
                L_00c8: ldc.i4.1 
                L_00c9: add 
                L_00ca: stloc.s CS$7$0001
                L_00cc: ldloc.s CS$7$0001
                L_00ce: ldloc.s CS$6$0000
                L_00d0: ldlen 
                L_00d1: conv.i4 
                L_00d2: clt 
                L_00d4: stloc.s CS$4$0002
                L_00d6: ldloc.s CS$4$0002
                L_00d8: brtrue L_001d
                L_00dd: ldloc.0 
                L_00de: call bool [mscorlib]System.String::IsNullOrEmpty(string)
                L_00e3: brtrue.s L_00f8
                L_00e5: ldloc.1 
                L_00e6: call bool [mscorlib]System.String::IsNullOrEmpty(string)
                L_00eb: brtrue.s L_00f8
                L_00ed: ldloc.2 
                L_00ee: call bool [mscorlib]System.String::IsNullOrEmpty(string)
                L_00f3: ldc.i4.0 
                L_00f4: ceq 
                L_00f6: br.s L_00f9
                L_00f8: ldc.i4.0 
                L_00f9: stloc.s CS$4$0002
                L_00fb: ldloc.s CS$4$0002
                L_00fd: brtrue.s L_0101
                L_00ff: br.s L_0141
                L_0101: ldarg.0 
                L_0102: ldloc.0 
                L_0103: ldloc.1 
                L_0104: call instance class [mscorlib]System.Type TestBootstrapper.ProgramRunner::LoadType(string, string)
                L_0109: stloc.s 'type'
                L_010b: ldloc.s 'type'
                L_010d: ldnull 
                L_010e: ceq 
                L_0110: ldc.i4.0 
                L_0111: ceq 
                L_0113: stloc.s CS$4$0002
                L_0115: ldloc.s CS$4$0002
                L_0117: brtrue.s L_011b
                L_0119: br.s L_0141
                L_011b: ldloc.s 'type'
                L_011d: ldloc.2 
                L_011e: ldc.i4.s 0x38
                L_0120: callvirt instance class [mscorlib]System.Reflection.MethodInfo [mscorlib]System.Type::GetMethod(string, valuetype [mscorlib]System.Reflection.BindingFlags)
                L_0125: stloc.s 'method'
                L_0127: ldloc.s 'method'
                L_0129: ldnull 
                L_012a: ceq 
                L_012c: ldc.i4.0 
                L_012d: ceq 
                L_012f: stloc.s CS$4$0002
                L_0131: ldloc.s CS$4$0002
                L_0133: brtrue.s L_0137
                L_0135: br.s L_0141
                L_0137: ldloc.s 'method'
                L_0139: ldnull 
                L_013a: ldnull 
                L_013b: callvirt instance object [mscorlib]System.Reflection.MethodBase::Invoke(object, object[])
                L_0140: pop 
                L_0141: ret 
                .try L_0068 to L_008e finally handler L_008e to L_00a2
            }
             */
            throw new NotImplementedException();
        }

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
        private void BuildProgramConstructor(AssemblyDefinition assembly, TypeDefinition programType, FieldReference assemblyLock, FieldReference executingAssembly, FieldReference loadedAssemblies, MethodReference currentDomain, MethodReference eventHandler, MethodReference assemblyResolve, MethodReference getExecutingAssembly, MethodReference resolveMethod)
        {
            var body = assembly.CreateDefaultConstructor(programType);
            body.MaxStack = 4;
            body.InitLocals = true;
            body.Variables.Add(new VariableDefinition(assembly.Import(typeof(AppDomain))));
            var il = body.CilWorker;

            //Inject anti reflector code
            InjectAntiReflectorCode(il);

            //define the assembly lock
            il.Append(il.Create(OpCodes.Stfld, assemblyLock));
            il.Append(OpCodes.Ldarg_0);
            var objConstructor = typeof(object).GetConstructor(Type.EmptyTypes);
            il.Append(il.Create(OpCodes.Call, assembly.Import(objConstructor)));
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ldarg_0);
            var cacheConstructor = typeof(Dictionary<string, Assembly>).GetConstructor(Type.EmptyTypes);
            il.Append(il.Create(OpCodes.Newobj, assembly.Import(cacheConstructor)));
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
        /// <param name="voidType">Type of the void.</param>
        /// <param name="resolveMethod">The resolve method.</param>
        /// <param name="startMethod">The start method.</param>
        /// <returns></returns>
        private MethodDefinition BuildMainMethod(AssemblyDefinition assembly, ICloakContext context, TypeReference programType, MethodReference getExecutingAssembly, MethodReference currentDomain, MethodReference eventHandler, MethodReference assemblyResolve, TypeReference voidType, MethodReference resolveMethod, MethodReference startMethod)
        {
            MethodDefinition entryPoint =
                new MethodDefinition(context.NameManager.GenerateName(NamingTable.Method),
                                     MethodAttributes.Private | MethodAttributes.Static |
                                     MethodAttributes.HideBySig, voidType);
            entryPoint.Body.InitLocals = true;
            entryPoint.Body.MaxStack = 4;
            //Initialise some locals
            entryPoint.AddLocal(typeof(AppDomain));
            entryPoint.AddLocal(typeof(Assembly));
            VariableDefinition result = new VariableDefinition(programType);
            entryPoint.Body.Variables.Add(result);

            //Add the method
            assembly.EntryPoint = entryPoint;

            //Declare the il to build the code
            CilWorker il = entryPoint.Body.CilWorker;

            //First of all add the anti reflector code
            InjectAntiReflectorCode(il);

            //Now output the code - essentially:
            /*
            AppDomain domain = AppDomain.CreateDomain("App");
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            ProgramRunner runner = (ProgramRunner)domain.CreateInstanceAndUnwrap(executingAssembly.FullName, "TestBootstrapper.ProgramRunner");
            AppDomain.CurrentDomain.AssemblyResolve += runner.ResolveAssembly;
            runner.Start();
             */
            il.Append(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Ldstr, context.NameManager.GenerateName(NamingTable.Type)));

            //Get the AppDomain::Create(string) method
            var appDomainCreate = typeof(AppDomain).GetMethod("Create", new[] { typeof(string) });
            il.Append(il.Create(OpCodes.Call, assembly.MainModule.Import(appDomainCreate)));
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
            //Start the program
            il.Append(il.Create(OpCodes.Callvirt, startMethod));
            il.Append(OpCodes.Nop);
            il.Append(OpCodes.Ret);
            return entryPoint;
        }

        /// <summary>
        /// Injects the anti reflector code.
        /// </summary>
        /// <param name="worker">The worker.</param>
        private void InjectAntiReflectorCode(CilWorker il)
        {

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
