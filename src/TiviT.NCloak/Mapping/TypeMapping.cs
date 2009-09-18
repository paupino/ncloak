using System.Collections.Generic;

namespace TiviT.NCloak.Mapping
{
    public class TypeMapping
    {
        private readonly string typeName;
        private readonly string obfuscatedTypeName;

        private readonly Dictionary<string, MemberMapping> methods;
        private readonly Dictionary<string, MemberMapping> properties;
        private readonly Dictionary<string, MemberMapping> fields;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeMapping"/> class.
        /// </summary>
        /// <param name="typeName">Name of the type.</param>
        /// <param name="obfuscatedTypeName">Name of the obfuscated type.</param>
        public TypeMapping(string typeName, string obfuscatedTypeName)
        {
            this.typeName = typeName;
            this.obfuscatedTypeName = obfuscatedTypeName;

            methods = new Dictionary<string, MemberMapping>();
            properties = new Dictionary<string, MemberMapping>();
            fields = new Dictionary<string, MemberMapping>();
        }

        /// <summary>
        /// Gets the name of the obfuscated type.
        /// </summary>
        /// <value>The name of the obfuscated type.</value>
        public string ObfuscatedTypeName
        {
            get { return obfuscatedTypeName; }
        }

        /// <summary>
        /// Gets the name of the type.
        /// </summary>
        /// <value>The name of the type.</value>
        public string TypeName
        {
            get { return typeName; }
        }

        /// <summary>
        /// Adds a new method name mapping.
        /// </summary>
        /// <param name="methodName">Name of the method.</param>
        /// <param name="obfuscatedMethodName">Name of the obfuscated method.</param>
        public void AddMethodMapping(string methodName, string obfuscatedMethodName)
        {
            methods.Add(methodName, new MemberMapping(methodName, obfuscatedMethodName));
        }

        /// <summary>
        /// Adds a new property name mapping.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="obfuscatedPropertyName">Name of the obfuscated property.</param>
        public void AddPropertyMapping(string propertyName, string obfuscatedPropertyName)
        {
            properties.Add(propertyName, new MemberMapping(propertyName, obfuscatedPropertyName));
        }

        /// <summary>
        /// Adds a new field name mapping.
        /// </summary>
        /// <param name="fieldName">Name of the field.</param>
        /// <param name="obfuscatedFieldName">Name of the obfuscated field.</param>
        public void AddFieldMapping(string fieldName, string obfuscatedFieldName)
        {
            fields.Add(fieldName, new MemberMapping(fieldName, obfuscatedFieldName));
        }

        /// <summary>
        /// Determines whether a method mapping exists for the specified method name.
        /// </summary>
        /// <param name="methodName">Name of the method.</param>
        /// <returns>
        /// 	<c>true</c> if a method mapping exists; otherwise, <c>false</c>.
        /// </returns>
        public bool HasMethodMapping(string methodName)
        {
            return methods.ContainsKey(methodName);
        }

        /// <summary>
        /// Determines whether a property mapping exists for the specified property name.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>
        /// 	<c>true</c> if a property mapping exists; otherwise, <c>false</c>.
        /// </returns>
        public bool HasPropertyMapping(string propertyName)
        {
            return properties.ContainsKey(propertyName);
        }

        /// <summary>
        /// Determines whether a field mapping exists for the specified field name.
        /// </summary>
        /// <param name="fieldName">Name of the field.</param>
        /// <returns>
        /// 	<c>true</c> if a field mapping exists; otherwise, <c>false</c>.
        /// </returns>
        public bool HasFieldMapping(string fieldName)
        {
            return fields.ContainsKey(fieldName);
        }

        /// <summary>
        /// Gets the name of the obfuscated method.
        /// </summary>
        /// <param name="methodName">Name of the original method.</param>
        /// <returns></returns>
        public string GetObfuscatedMethodName(string methodName)
        {
            if (HasMethodMapping(methodName))
                return methods[methodName].ObfuscatedMemberName;
            return null;
        }

        /// <summary>
        /// Gets the name of the obfuscated property.
        /// </summary>
        /// <param name="propertyName">Name of the original property.</param>
        /// <returns></returns>
        public string GetObfuscatedPropertyName(string propertyName)
        {
            if (HasPropertyMapping(propertyName))
                return properties[propertyName].ObfuscatedMemberName;
            return null;
        }

        /// <summary>
        /// Gets the name of the obfuscated field.
        /// </summary>
        /// <param name="fieldName">Name of the original field.</param>
        /// <returns></returns>
        public string GetObfuscatedFieldName(string fieldName)
        {
            if (HasFieldMapping(fieldName))
                return fields[fieldName].ObfuscatedMemberName;
            return null;
        }
    }
}
