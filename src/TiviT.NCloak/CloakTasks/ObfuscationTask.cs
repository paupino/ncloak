using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TiviT.NCloak.Mapping;

namespace TiviT.NCloak.CloakTasks
{
    public class ObfuscationTask : ICloakTask
    {
        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        public void RunTask(ICloakContext context)
        {
            //Loop through each assembly and obfuscate it
            foreach (string assembly in context.Settings.AssembliesToObfuscate)
            {
                Obfuscate(context, assembly);
            }
        }

        /// <summary>
        /// Performs obfuscation on the specified assembly.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assembly">The assembly.</param>
        private static void Obfuscate(ICloakContext context, string assembly)
        {
            //Get the assembly definition
            AssemblyDefinition definition = AssemblyFactory.GetAssembly(assembly);

            //Get the assembly mapping information (if any)
            if (context.MappingGraph.IsAssemblyMappingDefined(definition))
            {
                //Get the mapping object
                AssemblyMapping assemblyMapping = context.MappingGraph.GetAssemblyMapping(definition);

                //Go through each module
                foreach (ModuleDefinition moduleDefinition in definition.Modules)
                {
                    //Go through each type
                    foreach (TypeDefinition typeDefinition in moduleDefinition.Types)
                    {
                        //Get the type mapping
                        TypeMapping typeMapping = assemblyMapping.GetTypeMapping(typeDefinition.Name);
                        if (typeMapping == null)
                            continue; //There is a problem....
                        //Rename if necessary
                        if (!String.IsNullOrEmpty(typeMapping.ObfuscatedTypeName))
                            typeDefinition.Name = typeMapping.ObfuscatedTypeName;

                        //Go through each method
                        foreach (MethodDefinition methodDefinition in typeDefinition.Methods)
                        {
                            if (typeMapping.HasMethodMapping(methodDefinition.Name))
                                methodDefinition.Name = typeMapping.GetObfuscatedMethodName(methodDefinition.Name);

                            //Dive into the method body
                            UpdateMethodReferences(context, methodDefinition);
                        }

                        //Properties
                        foreach (PropertyDefinition propertyDefinition in typeDefinition.Properties)
                        {
                            if (typeMapping.HasPropertyMapping(propertyDefinition.Name))
                                propertyDefinition.Name =
                                    typeMapping.GetObfuscatedPropertyName(propertyDefinition.Name);

                            //Dive into the method body
                            if (propertyDefinition.GetMethod != null)
                                UpdateMethodReferences(context, propertyDefinition.GetMethod);
                            if (propertyDefinition.SetMethod != null)
                                UpdateMethodReferences(context, propertyDefinition.SetMethod);
                        }

                        //Fields
                        foreach (FieldDefinition fieldDefinition in typeDefinition.Fields)
                        {
                            if (typeMapping.HasFieldMapping(fieldDefinition.Name))
                                fieldDefinition.Name = typeMapping.GetObfuscatedFieldName(fieldDefinition.Name);
                        }

                    }
                }
            }

            //Save the assembly (ALWAYS)
            string outputPath = Path.Combine(context.Settings.OutputDirectory, Path.GetFileName(assembly));
            Console.WriteLine("Outputting assembly to {0}", outputPath);
            AssemblyFactory.SaveAssembly(definition, outputPath);
        }

        private static void UpdateMethodReferences(ICloakContext context, MethodDefinition methodDefinition)
        {
            if (methodDefinition.HasBody)
            {
                foreach (Instruction instruction in methodDefinition.Body.Instructions)
                {
                    //Find the call statement
                    switch (instruction.OpCode.Name)
                    {
                        case "call":
                        case "callvirt":
                        case "newobj":
#if DEBUG
                            Console.WriteLine("Discovered {0} {1} ({2})", instruction.OpCode.Name, instruction.Operand, instruction.Operand.GetType().Name);
#endif

                            //Look at the operand
                            if (instruction.Operand is GenericInstanceMethod) //We do this one first due to inheritance 
                            {
                                GenericInstanceMethod genericInstanceMethod =
                                    (GenericInstanceMethod) instruction.Operand;
                                //Update the standard naming
                                UpdateMemberTypeReferences(context, genericInstanceMethod);
                                //Update the generic types
                                foreach (TypeReference tr in genericInstanceMethod.GenericArguments)
                                    UpdateTypeReferences(context, tr);
                            }
                            else if (instruction.Operand is MethodReference)
                            {
                                MethodReference methodReference = (MethodReference)instruction.Operand;
                                //Update the standard naming
                                UpdateMemberTypeReferences(context, methodReference);
                            }

                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Updates the member type references.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="memberReference">The member reference.</param>
        private static void UpdateMemberTypeReferences(ICloakContext context, IMemberReference memberReference)
        {
            //Get the type reference for this
            TypeReference methodType = memberReference.DeclaringType;
            //Get the assembly for this
            if (methodType.Scope is AssemblyNameReference)
            {
                string assemblyName = ((AssemblyNameReference) methodType.Scope).FullName;
                //Check if this needs to be updated
                if (context.MappingGraph.IsAssemblyMappingDefined(assemblyName))
                {
                    AssemblyMapping assemblyMapping = context.MappingGraph.GetAssemblyMapping(assemblyName);
                    TypeMapping t = assemblyMapping.GetTypeMapping(methodType.Name);
                    if (t == null)
                        return; //No type defined

                    //Update the type name
                    if (!String.IsNullOrEmpty(t.ObfuscatedTypeName))
                        methodType.Name = t.ObfuscatedTypeName;

                    //We can't change method specifications....
                    if (memberReference is MethodSpecification)
                    {
                        MethodSpecification specification = (MethodSpecification)memberReference;
                        MethodReference meth = specification.GetOriginalMethod();
                        //Update the method name also if available
                        if (t.HasMethodMapping(meth.Name))
                            meth.Name = t.GetObfuscatedMethodName(meth.Name);
                    }
                    else
                    {
                        //Update the method name also if available
                        if (t.HasMethodMapping(memberReference.Name))
                            memberReference.Name = t.GetObfuscatedMethodName(memberReference.Name);
                    }
                }
            }
        }

        /// <summary>
        /// Updates the type references.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="typeReference">The type reference.</param>
        private static void UpdateTypeReferences(ICloakContext context, TypeReference typeReference)
        {
            //Get the assembly for this
            if (typeReference.Scope is AssemblyNameReference)
            {
                string assemblyName = ((AssemblyNameReference)typeReference.Scope).FullName;
                //Check if this needs to be updated
                if (context.MappingGraph.IsAssemblyMappingDefined(assemblyName))
                {
                    AssemblyMapping assemblyMapping = context.MappingGraph.GetAssemblyMapping(assemblyName);
                    TypeMapping t = assemblyMapping.GetTypeMapping(typeReference.Name);
                    if (t == null)
                        return; //No type defined

                    //Update the type name
                    if (!String.IsNullOrEmpty(t.ObfuscatedTypeName))
                        typeReference.Name = t.ObfuscatedTypeName;
                }
            }
        }
    }
}