using Mono.Cecil;

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
    }
}
