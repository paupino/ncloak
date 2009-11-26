using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

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
                case 0x10b:
                default:
                    return false;
                case 0x20b:
                    return true;
            }
        }
    }
}
