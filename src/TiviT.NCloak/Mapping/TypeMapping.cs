using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace TiviT.NCloak.Mapping
{
    public class TypeMapping
    {
        private readonly string typeName;
        private readonly string obfuscatedTypeName;

        private readonly Dictionary<string, MemberMapping> methods;
        private readonly Dictionary<string, MemberMapping> properties;
        private readonly Dictionary<string, MemberMapping> fields;

        private readonly Dictionary<string, MethodReference> obfuscatedMethods;
        private readonly Dictionary<string, PropertyReference> obfuscatedProperties;
        private readonly Dictionary<string, FieldReference> obfuscatedFields;

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

            obfuscatedMethods = new Dictionary<string, MethodReference>();
            obfuscatedProperties = new Dictionary<string, PropertyReference>();
            obfuscatedFields = new Dictionary<string, FieldReference>();
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
        /// <param name="method">The original method reference.</param>
        /// <param name="obfuscatedMethodName">Name of the obfuscated method.</param>
        public void AddMethodMapping(MethodReference method, string obfuscatedMethodName)
        {
            if (method == null) throw new ArgumentNullException("method");
            string methodName = method.Name;
            if (!methods.ContainsKey(methodName))
            {
                methods.Add(methodName, new MemberMapping(methodName, obfuscatedMethodName));
                obfuscatedMethods.Add(obfuscatedMethodName, method);
            }
        }

        /// <summary>
        /// Adds a new property name mapping.
        /// </summary>
        /// <param name="property">The original property reference.</param>
        /// <param name="obfuscatedPropertyName">Name of the obfuscated property.</param>
        public void AddPropertyMapping(PropertyReference property, string obfuscatedPropertyName)
        {
            if (property == null) throw new ArgumentNullException("property");
            string propertyName = property.Name;
            if (!properties.ContainsKey(propertyName))
            {
                properties.Add(propertyName, new MemberMapping(propertyName, obfuscatedPropertyName));
                obfuscatedProperties.Add(obfuscatedPropertyName, property);
            }
        }

        /// <summary>
        /// Adds a new field name mapping.
        /// </summary>
        /// <param name="field">The original field reference.</param>
        /// <param name="obfuscatedFieldName">Name of the obfuscated field.</param>
        public void AddFieldMapping(FieldReference field, string obfuscatedFieldName)
        {
            if (field == null) throw new ArgumentNullException("field");
            string fieldName = field.Name;
            if (!fields.ContainsKey(fieldName))
            {
                fields.Add(fieldName, new MemberMapping(fieldName, obfuscatedFieldName));
                obfuscatedFields.Add(obfuscatedFieldName, field);
            }
        }

        /// <summary>
        /// Determines whether a method mapping exists for the specified method name.
        /// </summary>
        /// <param name="method">The original method reference.</param>
        /// <returns>
        /// 	<c>true</c> if a method mapping exists; otherwise, <c>false</c>.
        /// </returns>
        public bool HasMethodMapping(MethodReference method)
        {
            if (method == null) throw new ArgumentNullException("method");
            string methodName = method.Name;
            return methods.ContainsKey(methodName);
        }

        /// <summary>
        /// Determines whether a property mapping exists for the specified property name.
        /// </summary>
        /// <param name="property">The original property reference.</param>
        /// <returns>
        /// 	<c>true</c> if a property mapping exists; otherwise, <c>false</c>.
        /// </returns>
        public bool HasPropertyMapping(PropertyReference property)
        {
            if (property == null) throw new ArgumentNullException("property");
            string propertyName = property.Name;
            return properties.ContainsKey(propertyName);
        }

        /// <summary>
        /// Determines whether a field mapping exists for the specified field name.
        /// </summary>
        /// <param name="field">The original field reference.</param>
        /// <returns>
        /// 	<c>true</c> if a field mapping exists; otherwise, <c>false</c>.
        /// </returns>
        public bool HasFieldMapping(FieldReference field)
        {
            if (field == null) throw new ArgumentNullException("field");
            string fieldName = field.Name;
            return fields.ContainsKey(fieldName);
        }

        /// <summary>
        /// Gets the name of the obfuscated method.
        /// </summary>
        /// <param name="method">The original method reference.</param>
        /// <returns></returns>
        public string GetObfuscatedMethodName(MethodReference method)
        {
            if (method == null) throw new ArgumentNullException("method");
            if (HasMethodMapping(method))
            {
                string methodName = method.Name;
                return methods[methodName].ObfuscatedMemberName;
            }
            return null;
        }

        /// <summary>
        /// Gets the name of the obfuscated property.
        /// </summary>
        /// <param name="property">The original property reference.</param>
        /// <returns></returns>
        public string GetObfuscatedPropertyName(PropertyReference property)
        {
            if (property == null) throw new ArgumentNullException("property");
            if (HasPropertyMapping(property))
            {
                string propertyName = property.Name;
                return properties[propertyName].ObfuscatedMemberName;
            }
            return null;
        }

        /// <summary>
        /// Gets the name of the obfuscated field.
        /// </summary>
        /// <param name="field">The original field reference.</param>
        /// <returns></returns>
        public string GetObfuscatedFieldName(FieldReference field)
        {
            if (field == null) throw new ArgumentNullException("field");
            if (HasFieldMapping(field))
            {
                string fieldName = field.Name;
                return fields[fieldName].ObfuscatedMemberName;
            }
            return null;
        }

        /// <summary>
        /// Determines whether the specified method name has already been obfuscated
        /// </summary>
        /// <param name="obfuscatedMethodName">Name of the obfuscated method.</param>
        /// <returns>
        /// 	<c>true</c> if the method has been obfuscated; otherwise, <c>false</c>.
        /// </returns>
        public bool HasMethodBeenObfuscated(string obfuscatedMethodName)
        {
            return obfuscatedMethods.ContainsKey(obfuscatedMethodName);
        }

        /// <summary>
        /// Determines whether the specified property name has already been obfuscated
        /// </summary>
        /// <param name="obfuscatedPropertyName">Name of the obfuscated property.</param>
        /// <returns>
        /// 	<c>true</c> if the property has been obfuscated; otherwise, <c>false</c>.
        /// </returns>
        public bool HasPropertyBeenObfuscated(string obfuscatedPropertyName)
        {
            return obfuscatedProperties.ContainsKey(obfuscatedPropertyName);
        }

        /// <summary>
        /// Determines whether the specified field name has already been obfuscated
        /// </summary>
        /// <param name="obfuscatedFieldName">Name of the obfuscated field.</param>
        /// <returns>
        /// 	<c>true</c> if the field has been obfuscated; otherwise, <c>false</c>.
        /// </returns>
        public bool HasFieldBeenObfuscated(string obfuscatedFieldName)
        {
            return obfuscatedFields.ContainsKey(obfuscatedFieldName);
        }
    }
}
