using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Reflection;
using MethodAttributes=Mono.Cecil.MethodAttributes;
using MethodBody=Mono.Cecil.Cil.MethodBody;

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
            switch (assemblyDefinition.MainModule.Image.DebugHeader.Magic)
            {
                case 0x10b: //0x10b is 32 bit
                default:
                    return false;
                case 0x20b:
                    return true;
            }
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
    }
}
