#define VERBOSE
using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace TiviT.NCloak.CloakTasks
{
    public class StringEncryptionTask : ICloakTask
    {
        private readonly StringEncryptionMethod method;
        private readonly Random random;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEncryptionTask"/> class.
        /// </summary>
        public StringEncryptionTask()
            : this(StringEncryptionMethod.Xor)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringEncryptionTask"/> class.
        /// </summary>
        /// <param name="method">The method.</param>
        public StringEncryptionTask(StringEncryptionMethod method)
        {
            this.method = method;
            random = new Random();
        }

        /// <summary>
        /// Gets the task name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "Encrypting strings"; }
        }

        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        /// <param name="context">The running context of this cloak job.</param>
        public void RunTask(ICloakContext context)
        {
            //Go through each assembly and encrypt the strings 
            //for each assembly inject a decryption routine - we'll let the obfuscator hide it properly
            //Loop through each assembly and obfuscate it
            foreach (AssemblyDefinition definition in context.GetAssemblyDefinitions().Values)
            {
                EncryptStringsInAssembly(definition);
            }
        }

        /// <summary>
        /// Encrypts the strings within the given assembly.
        /// </summary>
        /// <param name="definition">The assembly definition.</param>
        private void EncryptStringsInAssembly(AssemblyDefinition definition)
        {
            //Add an encryption function
            MethodReference decryptionMethod = null;

            //Generate a new type for decryption
            foreach (TypeDefinition td in definition.MainModule.Types)
                if (td.Name == "<Module>")
                {
                    MethodDefinition md = new MethodDefinition("Decrypt", MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.Compilercontrolled, definition.Import(typeof(string)));

                    //Generate the parameters
                    md.Parameters.Add(new ParameterDefinition("v", 0, ParameterAttributes.None, definition.Import(typeof(string))));
                    md.Parameters.Add(new ParameterDefinition("s", 1, ParameterAttributes.None, definition.Import(typeof(int))));

                    //Add it
                    td.Methods.Add(md);

                    //Output the encryption method body
                    switch (method)
                    {
                        case StringEncryptionMethod.Xor:
                            GenerateXorDecryptionMethod(definition, md.Body);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    //Finally get the reference
                    decryptionMethod = md.GetOriginalMethod();
                }

            //Loop through the modules
            foreach (ModuleDefinition moduleDefinition in definition.Modules)
            {
                //Go through each type
                foreach (TypeDefinition typeDefinition in moduleDefinition.Types)
                {
                    //Go through each method
                    foreach (MethodDefinition methodDefinition in typeDefinition.Methods)
                    {
                        if (methodDefinition.HasBody)
                        {
                            OutputHelper.WriteMethod(typeDefinition, methodDefinition);
                            ProcessInstructions(definition, methodDefinition.Body, decryptionMethod);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates the xor decryption method.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="body">The body.</param>
        private static void GenerateXorDecryptionMethod(AssemblyDefinition assembly, MethodBody body)
        {
            CilWorker worker = body.CilWorker;

            //Generate the decryption method
            //Since this is XOR it is the same as the encryption method
            //In reality its a bit of a joke calling this encryption as its really
            //just obfusaction
            /*
                char[] characters = value.ToCharArray();
                for (int i = 0; i < characters.Length; i++)
                {
                    characters[i] = (char)(characters[i] ^ salt);
                }
                return new String(characters);
             */
            
            //Declare a local to store the char array
            body.InitLocals = true;
            body.Method.AddLocal(typeof(char[]));
            body.Method.AddLocal(typeof(int));
            body.Method.AddLocal(typeof(string));
            body.Method.AddLocal(typeof(bool));
            
            //Start with a nop
            worker.Append(worker.Create(OpCodes.Nop));

            //Load the first argument into the register
            Instruction ldArg0 = worker.Create(OpCodes.Ldarg_0);
            worker.Append(ldArg0);

            //Call ToCharArray on this -- need to find it first
            var toCharArrayMethodRef = assembly.Import(typeof(string).GetMethod("ToCharArray", Type.EmptyTypes));
            //Import the method first
            toCharArrayMethodRef = body.ImportMethod(toCharArrayMethodRef);
            Instruction toCharArray = worker.Create(OpCodes.Callvirt, toCharArrayMethodRef);
            worker.Append(toCharArray);
            //Store it in the first local
            Instruction stLoc0 = worker.Create(OpCodes.Stloc_0);
            worker.Append(stLoc0);

            //Set up the loop
            worker.Append(worker.Create(OpCodes.Ldc_I4_0));
            Instruction stLoc1 = worker.Create(OpCodes.Stloc_1);
            worker.Append(stLoc1);
            
            //We'll insert a br.s here later....

            //Insert another nop and do the rest of our loop
            Instruction loopNop = worker.Create(OpCodes.Nop);
            worker.Append(loopNop);
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldelem_U2)); //Load the array
            worker.Append(worker.Create(OpCodes.Ldarg_1));
            worker.Append(worker.Create(OpCodes.Xor)); //Do the xor
            worker.Append(worker.Create(OpCodes.Conv_U2));
            worker.Append(worker.Create(OpCodes.Stelem_I2)); //Store back in the array
            worker.Append(worker.Create(OpCodes.Nop));
            worker.Append(worker.Create(OpCodes.Ldloc_1));
            worker.Append(worker.Create(OpCodes.Ldc_I4_1));
            worker.Append(worker.Create(OpCodes.Add));
            worker.Append(worker.Create(OpCodes.Stloc_1));
            Instruction ldLoc = worker.Create(OpCodes.Ldloc_1);
            worker.Append(ldLoc);
            //Link to this line from an earlier statement
            worker.InsertAfter(stLoc1, worker.Create(OpCodes.Br_S, ldLoc));
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            worker.Append(worker.Create(OpCodes.Ldlen));
            worker.Append(worker.Create(OpCodes.Conv_I4));
            worker.Append(worker.Create(OpCodes.Clt)); //i < array.Length
            worker.Append(worker.Create(OpCodes.Stloc_3));
            worker.Append(worker.Create(OpCodes.Ldloc_3));
            worker.Append(worker.Create(OpCodes.Brtrue_S, loopNop)); //Do the loop

            //Return a new string
            worker.Append(worker.Create(OpCodes.Ldloc_0));
            //Find the constructor we want to use
            MethodReference constructor = assembly.Import(typeof(string).GetConstructor(new [] { typeof(char[])}));
            constructor = body.ImportMethod(constructor);
            worker.Append(worker.Create(OpCodes.Newobj, constructor));
            Instruction stloc2 = worker.Create(OpCodes.Stloc_2);
            worker.Append(stloc2);
            Instruction ldloc2 = worker.Create(OpCodes.Ldloc_2);
            worker.Append(ldloc2);
            worker.InsertAfter(stloc2, worker.Create(OpCodes.Br_S, ldloc2));
            worker.Append(worker.Create(OpCodes.Ret));
        }

        /// <summary>
        /// Processes the instructions replacing all strings being loaded with an encrypted version.
        /// </summary>
        /// <param name="assemblyDef">The assembly definition.</param>
        /// <param name="body">The body.</param>
        /// <param name="decryptMethod">The decrypt method.</param>
        private void ProcessInstructions(AssemblyDefinition assemblyDef, MethodBody body, MethodReference decryptMethod)
        {
            InstructionCollection instructions = body.Instructions;
            CilWorker il = body.CilWorker;

            List<Instruction> instructionsToExpand = new List<Instruction>();
            List<int> offsets = new List<int>();
            foreach (Instruction instruction in instructions)
            {
                //Find the call statement
                switch (instruction.OpCode.Name)
                {
                    case "ldstr":
                        //We've found a string load message - we need to replace this instruction
                        if (instruction.Operand is string) //Only do the direct strings for now
                            instructionsToExpand.Add(instruction);
                        break;
                }
            }
            //Fix each ldstr instruction found
            foreach (Instruction instruction in instructionsToExpand)
            {
                //What we do is replace the ldstr "bla" with:
                //ldstr bytearray encrypted_array
                //ldc.i4 random_integer
                //call string class Decrypt(string, int32)

                //First get the original value
                string originalValue = instruction.Operand.ToString();
                offsets.Add(instruction.Offset);

                //Secondly generate a random integer as a salt
                int salt = random.Next(5000, 10000);

                //Now we need to work out what the encrypted value is and set the operand
                OutputHelper.WriteLine("Encrypting string \"{0}\"", originalValue);
                string byteArray = EncryptString(originalValue, salt);
                Instruction loadString = il.Create(OpCodes.Ldstr, byteArray);
                il.Replace(instruction, loadString);

                //Now load the salt
                Instruction loadSalt = il.Create(OpCodes.Ldc_I4, salt);
                il.InsertAfter(loadString, loadSalt);

                //Process the decryption
                Instruction call = il.Create(OpCodes.Call, decryptMethod);
                il.InsertAfter(loadSalt, call);
            }

            //Unfortunately one thing Mono.Cecil doesn't do is adjust instruction offsets for branch statements
            //and exception handling start points. We need to fix these manually
            if (offsets.Count == 0)
                return;

            //Do the adjustments
            il.AdjustOffsets(body, offsets, 6 + assemblyDef.GetAddressSize());
        }

        /// <summary>
        /// Encrypts the string using the selected encryption method.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="salt">The salt.</param>
        /// <returns></returns>
        private string EncryptString(string value, int salt)
        {
            switch (method)
            {
                case StringEncryptionMethod.Xor:
                    return EncryptWithXor(value, salt);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Encrypts the string with the xor method.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="salt">The salt.</param>
        /// <returns></returns>
        private static string EncryptWithXor(string value, int salt)
        {
            char[] characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                characters[i] = (char)(characters[i] ^ salt);
            }
            return new String(characters);
        }
    }
}
