using Mono.Cecil;
using TiviT.NCloak.Mapping;

namespace TiviT.NCloak.CloakTasks
{
    public class MappingTask : ICloakTask
    {
        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        /// <param name="context">The running context of this cloak job.</param>
        public void RunTask(ICloakContext context)
        {
            //Go through the members and build up a mapping graph
            //If this is done then the members in the graph will be obfuscated, otherwise we'll 
            //just obfuscate private members

            //Loop through each assembly and process it
            foreach (string assembly in context.Settings.AssembliesToObfuscate)
            {
                ProcessAssembly(context, assembly);
            }
        }

        /// <summary>
        /// Processes the assembly - goes through each member and applies a mapping.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The assembly.</param>
        private static void ProcessAssembly(ICloakContext context, string assembly)
        {
            //Store whether to obfuscate all members
            bool obfuscateAll = context.Settings.ObfuscateAllModifiers;

            //Get the assembly definition
            AssemblyDefinition definition = AssemblyFactory.GetAssembly(assembly);

            //Set up the mapping graph
            AssemblyMapping assemblyMapping = context.MappingGraph.AddAssembly(definition);

            //Get a reference to the name manager
            NameManager nameManager = context.NameManager;

            //Go through each module
            foreach (ModuleDefinition moduleDefinition in definition.Modules)
            {
                //Go through each type
                foreach (TypeDefinition typeDefinition in moduleDefinition.Types)
                {
                    TypeMapping typeMapping;
                    if (obfuscateAll)
                        typeMapping = assemblyMapping.AddType(typeDefinition.Name, nameManager.GenerateName(NamingTable.Type));
                    else
                        typeMapping = assemblyMapping.AddType(typeDefinition.Name, null);

                    //Go through each method
                    foreach (MethodDefinition methodDefinition in typeDefinition.Methods)
                    {
                        if (obfuscateAll)
                        {
                            //TODO Take into account whether this is overriden, or an interface implementation
                            typeMapping.AddMethodMapping(methodDefinition.Name,
                                                         nameManager.GenerateName(NamingTable.Method));
                        }
                        else if (methodDefinition.IsPrivate)
                            typeMapping.AddMethodMapping(methodDefinition.Name, nameManager.GenerateName(NamingTable.Method));
                    }

                    //Properties
                    foreach (PropertyDefinition propertyDefinition in typeDefinition.Properties)
                    {
                        if (obfuscateAll)
                        {
                            //TODO Take into account whether this is overriden, or an interface implementation
                            typeMapping.AddPropertyMapping(propertyDefinition.Name,
                                                           nameManager.GenerateName(NamingTable.Property));
                        }
                        else if (propertyDefinition.GetMethod != null && propertyDefinition.SetMethod != null)
                        {
                            //Both parts need to be private
                            if (propertyDefinition.GetMethod.IsPrivate && propertyDefinition.SetMethod.IsPrivate)
                                typeMapping.AddPropertyMapping(propertyDefinition.Name, nameManager.GenerateName(NamingTable.Property));
                        }
                        else if (propertyDefinition.GetMethod != null)
                        {
                            //Only the get is present - make sure it is private
                            if (propertyDefinition.GetMethod.IsPrivate)
                                typeMapping.AddPropertyMapping(propertyDefinition.Name, nameManager.GenerateName(NamingTable.Property));
                        }
                        else if (propertyDefinition.SetMethod != null)
                        {
                            //Only the set is present - make sure it is private
                            if (propertyDefinition.SetMethod.IsPrivate)
                                typeMapping.AddPropertyMapping(propertyDefinition.Name, nameManager.GenerateName(NamingTable.Property));
                        }
                    }

                    //Fields
                    foreach (FieldDefinition fieldDefinition in typeDefinition.Fields)
                    {
                        if (obfuscateAll)
                            typeMapping.AddFieldMapping(fieldDefinition.Name, nameManager.GenerateName(NamingTable.Field));
                        else if (fieldDefinition.IsPrivate)
                        {
                            //Rename if private
                            typeMapping.AddFieldMapping(fieldDefinition.Name, nameManager.GenerateName(NamingTable.Field));
                        }
                    }
                }
            }
        }
    }
}
