#define VERBOSE
using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Reflection;
using MethodAttributes=Mono.Cecil.MethodAttributes;
using MethodBody=Mono.Cecil.Cil.MethodBody;
using System.Collections.Generic;

namespace TiviT.NCloak
{
    public static class CecilExtensions
    {
        /// <summary>
        /// Gets the type definition.
        /// </summary>
        /// <param name="typeReference">The type reference.</param>
        /// <returns></returns>
        public static TypeDefinition GetTypeDefinition(this TypeReference typeReference)
        {
            if (typeReference == null)
                return null;
            foreach (TypeDefinition td in typeReference.Module.Types)
                if (td.FullName == typeReference.FullName)
                    return td;
            return null;
        }

        public static MethodDefinition FindMethod(this MethodDefinitionCollection methods, string methodName, ParameterDefinitionCollection parameters)
        {
            MethodDefinition[] defs = methods.GetMethod(methodName);
            foreach (MethodDefinition def in defs)
            {
                //Check the signature
                if (def.Parameters.Count == parameters.Count)
                {
                    bool isMatch = true;
                    for (int i = 0; i < def.Parameters.Count && isMatch; i++)
                    {
                        if (def.Parameters[i].ParameterType.Name != parameters[i].ParameterType.Name)
                            isMatch = false;
                    }

                    //If we are a match then return the type
                    if (isMatch)
                        return def;
                }
            }
            return null;
        }

        public static VariableDefinition AddLocal(this MethodDefinition methodDef, Type localType)
        {
            TypeReference declaringType = methodDef.DeclaringType;
            ModuleDefinition module = declaringType.Module;
            TypeReference variableType = module.Import(localType);
            VariableDefinition result = new VariableDefinition(variableType);

            methodDef.Body.Variables.Add(result);

            return result;
        }

        public static VariableDefinition AddLocal(this MethodDefinition methodDef, AssemblyDefinition assembly, Type localType)
        {
            TypeReference variableType = assembly.MainModule.Import(localType);
            VariableDefinition result = new VariableDefinition(variableType);

            methodDef.Body.Variables.Add(result);

            return result;
        }

        public static TypeReference GetTypeReference(this MethodDefinition methodDef, Type localType)
        {
            TypeReference declaringType = methodDef.DeclaringType;
            ModuleDefinition module = declaringType.Module;
            return module.Import(localType);
        }

        public static MethodReference ImportMethod(this MethodBody body, MethodReference reference)
        {
            return body.Method.DeclaringType.Module.Import(reference);
        }

        public static int GetAddressSize(this AssemblyDefinition assemblyDefinition)
        {
            if (Is64BitAssembly(assemblyDefinition))
                return 8;
            return 4;
        }

        public static bool Is64BitAssembly(this AssemblyDefinition assemblyDefinition)
        {
            if (assemblyDefinition == null) throw new ArgumentNullException("assemblyDefinition");
            // ReSharper disable RedundantCaseLabel
            switch (assemblyDefinition.MainModule.Image.DebugHeader.Magic)
            {
                case 0x10b: //0x10b is 32 bit
                default:
                    return false;
                case 0x20b:
                    return true;
            }
            // ReSharper restore RedundantCaseLabel
        }

        public static TypeReference Import(this AssemblyDefinition assemblyDefinition, Type type)
        {
            if (assemblyDefinition == null) throw new ArgumentNullException("assemblyDefinition");
            return assemblyDefinition.MainModule.Import(type);
        }

        public static MethodReference Import(this AssemblyDefinition assemblyDefinition, MethodBase methodBase)
        {
            if (assemblyDefinition == null) throw new ArgumentNullException("assemblyDefinition");
            return assemblyDefinition.MainModule.Import(methodBase);
        }

        /// <summary>
        /// Creates the default constructor.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="typeDefinition">The type definition.</param>
        public static MethodBody CreateDefaultConstructor(this AssemblyDefinition assembly, TypeDefinition typeDefinition)
        {
            MethodDefinition ctor = new MethodDefinition(".ctor",
                                             MethodAttributes.Public | MethodAttributes.HideBySig |
                                             MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                                             assembly.Import(typeof(void)));
            typeDefinition.Constructors.Add(ctor);

            //Also define the call to the base type (object)
            return ctor.Body;
        }

        /// <summary>
        /// Appends a basic op code.
        /// </summary>
        /// <param name="il">The il.</param>
        /// <param name="opCode">The op code.</param>
        public static void Append(this CilWorker il, OpCode opCode)
        {
            il.Append(il.Create(opCode));
        }

        public static void AdjustOffsets(this CilWorker il, MethodBody body, int adjustBy)
        {
            //Adjust everything over 0
            AdjustOffsets(il, body, new List<int> { 0 }, adjustBy);
        }

        public static void AdjustOffsets(this CilWorker il, MethodBody body, IList<int> offsets, int adjustBy)
        {
            //Unfortunately one thing Mono.Cecil doesn't do is adjust instruction offsets for branch statements
            //and exception handling start points. We need to fix these manually
            if (offsets.Count == 0)
                return;

            //We need to make sure we don't fix any instructions twice!
            List<int> seenHashCodes = new List<int>();

            //Fix all branch statements
            for (int i = 0; i < body.Instructions.Count; i++)
            {
                //Get the instruction
                Instruction instruction = body.Instructions[i];

                //We need to find the target as it may have changed
                if (instruction.Operand is Instruction)
                {
                    Instruction target = (Instruction)instruction.Operand;
                    int hashCode = target.GetHashCode();
                    if (seenHashCodes.Contains(hashCode))
                        continue;
                    seenHashCodes.Add(hashCode);

                    OpCode opCode = instruction.OpCode;

                    //Work out the new offset
                    int originalOffset = target.Offset;
                    int offset = target.Offset;
                    foreach (int movedOffsets in offsets)
                    {
                        if (originalOffset > movedOffsets)
                            offset += adjustBy;
                    }
                    target.Offset = offset;
#if VERBOSE
                    OutputHelper.WriteLine("Shifting {0} from {1:x} to {2:x}", opCode.Name, originalOffset, offset);
#endif
                    Instruction newInstr = il.Create(opCode, target);
                    il.Replace(instruction, newInstr);
                }
                else if (instruction.Operand is Instruction[]) //e.g. Switch statements
                {
                    Instruction[] targets = (Instruction[])instruction.Operand;
#if VERBOSE
                    OutputHelper.WriteLine("Shifting {0} from:", instruction.OpCode.Name);
#endif
                    foreach (Instruction target in targets)
                    {
                        int hashCode = target.GetHashCode();
                        if (seenHashCodes.Contains(hashCode))
                            continue;
                        seenHashCodes.Add(hashCode);

                        //Work out the new offset
                        int originalOffset = target.Offset;
                        int offset = target.Offset;
                        foreach (int movedOffsets in offsets)
                        {
                            if (originalOffset > movedOffsets)
                                offset += adjustBy;
                        }
                        target.Offset = offset;
#if VERBOSE
                        OutputHelper.WriteLine("\t{0:x} to {1:x}", originalOffset, offset);
#endif
                    }
                    Instruction newInstr = il.Create(instruction.OpCode, targets);
                    il.Replace(instruction, newInstr);
                }
            }
            //If there is a try adjust the starting point also
            foreach (ExceptionHandler handler in body.ExceptionHandlers)
            {
                //Work out the new offset
                Instruction target = handler.TryStart;
                int hashCode = target.GetHashCode();
                if (seenHashCodes.Contains(hashCode))
                    continue;
                seenHashCodes.Add(hashCode);

                int originalOffset = target.Offset;
                int offset = target.Offset;
                foreach (int movedOffsets in offsets)
                {
                    if (originalOffset > movedOffsets)
                        offset += adjustBy;
                }
#if VERBOSE
                OutputHelper.WriteLine("Shifting try start from {0:x} to {1:x}", originalOffset, offset);
#endif
                target.Offset = offset;
            }
        }
    }
}
