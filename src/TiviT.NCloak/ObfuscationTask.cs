using System;
using System.IO;
using Mono.Cecil;

namespace TiviT.NCloak
{
    public class ObfuscationTask : CloakTaskBase
    {
        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        protected override void RunTask()
        {
            //Loop through each assembly and obfuscate it
            foreach (string assembly in Settings.AssembliesToObfuscate)
            {
                Obfuscate(assembly);
            }
        }

        /// <summary>
        /// Obfuscates the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        private void Obfuscate(string assembly)
        {
            //Get the assembly definition
            AssemblyDefinition definition = AssemblyFactory.GetAssembly(assembly);

            //Keep a dirty bit for saving
            bool isDirty = false;

            //Go through each module
            foreach (ModuleDefinition moduleDefinition in definition.Modules)
            {
                //Go through each type
                foreach (TypeDefinition typeDefinition in moduleDefinition.Types)
                {
                    //We can't rename types yet - we don't know enough!

                    //Go through each method
                    foreach (MethodDefinition methodDefinition in typeDefinition.Methods)
                    {
                        if (methodDefinition.IsPrivate)
                        {
                            //Rename
                            methodDefinition.Name = Settings.NameManager.GenerateName(NamingTable.Method);
                            isDirty = true;
                        }
                    }

                    //Properties
                    foreach (PropertyDefinition propertyDefinition in typeDefinition.Properties)
                    {
                        //Rename only if the whole property is private
                        if (propertyDefinition.GetMethod != null && propertyDefinition.SetMethod != null)
                        {
                            //Both parts need to be private
                            if (propertyDefinition.GetMethod.IsPrivate && propertyDefinition.SetMethod.IsPrivate)
                            {
                                //Rename
                                propertyDefinition.Name = Settings.NameManager.GenerateName(NamingTable.Property);
                                isDirty = true;
                            }
                        }
                        else if (propertyDefinition.GetMethod != null)
                        {
                            //Only the get is present - make sure it is private
                            if (propertyDefinition.GetMethod.IsPrivate)
                            {
                                //Rename
                                propertyDefinition.Name = Settings.NameManager.GenerateName(NamingTable.Property);
                                isDirty = true;
                            }
                        }
                        else if (propertyDefinition.SetMethod != null)
                        {
                            //Only the set is present - make sure it is private
                            if (propertyDefinition.SetMethod.IsPrivate)
                            {
                                //Rename
                                propertyDefinition.Name = Settings.NameManager.GenerateName(NamingTable.Property);
                                isDirty = true;
                            }
                        }
                    }

                    //Fields
                    foreach (FieldDefinition fieldDefinition in typeDefinition.Fields)
                    {
                        //Rename if private
                        if (fieldDefinition.IsPrivate)
                        {
                            fieldDefinition.Name = Settings.NameManager.GenerateName(NamingTable.Field);
                            isDirty = true;
                        }
                    }
                }
            }

            //Save the assembly if it is dirty
            if (isDirty)
            {
                string outputPath = Path.Combine(Settings.OutputDirectory, Path.GetFileName(assembly));
                Console.WriteLine("Outputting assembly to {0}", outputPath);
                AssemblyFactory.SaveAssembly(definition, outputPath);
            }
        }
    }
}
