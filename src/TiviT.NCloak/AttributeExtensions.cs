using Mono.Cecil;

namespace TiviT.NCloak
{
    public static class AttributeExtensions
    {
        /// <summary>
        /// Determines whether the specified type attributes is private.
        /// </summary>
        /// <param name="typeDefinition">The type definition.</param>
        /// <returns>
        /// 	<c>true</c> if the specified type attributes is private; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsPrivate(this TypeDefinition typeDefinition)
        {
            return (typeDefinition.Attributes & TypeAttributes.NotPublic) > 0;
        }
    }
}
