using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TiviT.NCloak.Mapping;

namespace TiviT.NCloak.CloakTasks
{
    public class ObfuscationTask : ICloakTask
    {
        /// <summary>
        /// Gets the task name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return "Obfuscating members"; }
        }

        /// <summary>
        /// Runs the specified cloaking task.
        /// </summary>
        public void RunTask(ICloakContext context)
        {
            //Loop through each assembly and obfuscate it
            foreach (AssemblyDefinition definition in context.GetAssemblyDefinitions().Values)
            {
                Obfuscate(context, definition);
            }
        }

        /// <summary>
        /// Performs obfuscation on the specified assembly.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="definition">The assembly definition.</param>
        private static void Obfuscate(ICloakContext context, AssemblyDefinition definition)
        {
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
                        TypeMapping typeMapping = assemblyMapping.GetTypeMapping(typeDefinition);
                        if (typeMapping == null)
                            continue; //There is a problem....
                        //Rename if necessary
                        if (!String.IsNullOrEmpty(typeMapping.ObfuscatedTypeName))
                            typeDefinition.Name = typeMapping.ObfuscatedTypeName;

                        //Go through each method
                        foreach (MethodDefinition methodDefinition in typeDefinition.Methods)
                        {
                            if (typeMapping.HasMethodMapping(methodDefinition))
                                methodDefinition.Name = typeMapping.GetObfuscatedMethodName(methodDefinition);

                            //Dive into the method body
                            UpdateMethodReferences(context, methodDefinition);
                        }

                        //Properties
                        foreach (PropertyDefinition propertyDefinition in typeDefinition.Properties)
                        {
                            if (typeMapping.HasPropertyMapping(propertyDefinition))
                                propertyDefinition.Name =
                                    typeMapping.GetObfuscatedPropertyName(propertyDefinition);

                            //Dive into the method body
                            if (propertyDefinition.GetMethod != null)
                                UpdateMethodReferences(context, propertyDefinition.GetMethod);
                            if (propertyDefinition.SetMethod != null)
                                UpdateMethodReferences(context, propertyDefinition.SetMethod);
                        }

                        //Fields
                        foreach (FieldDefinition fieldDefinition in typeDefinition.Fields)
                        {
                            if (typeMapping.HasFieldMapping(fieldDefinition))
                                fieldDefinition.Name = typeMapping.GetObfuscatedFieldName(fieldDefinition);
                        }

                    }
                }
            }
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
                            OutputHelper.WriteLine("Discovered {0} {1} ({2})", instruction.OpCode.Name, instruction.Operand, instruction.Operand.GetType().Name);
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
                        case "stfld":
                        case "ldfld":
#if DEBUG
                            OutputHelper.WriteLine("Discovered {0} {1} ({2})", instruction.OpCode.Name, instruction.Operand, instruction.Operand.GetType().Name);
#endif
                            //Look at the operand
                            FieldReference fieldReference = instruction.Operand as FieldReference;
                            if (fieldReference != null)
                                UpdateMemberTypeReferences(context, fieldReference);
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
                    TypeMapping t = assemblyMapping.GetTypeMapping(methodType);
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
                        if (t.HasMethodMapping(meth))
                            meth.Name = t.GetObfuscatedMethodName(meth);
                    }
                    else if (memberReference is FieldReference)
                    {
                        FieldReference fr = (FieldReference) memberReference;
                        if (t.HasFieldMapping(fr))
                            memberReference.Name = t.GetObfuscatedFieldName(fr);
                    }
                    else if (memberReference is MethodReference) //Is this ever used?? Used to be just an else without if
                    {
                        MethodReference mr = (MethodReference) memberReference;
                        //Update the method name also if available
                        if (t.HasMethodMapping(mr))
                            memberReference.Name = t.GetObfuscatedMethodName(mr);
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
                    TypeMapping t = assemblyMapping.GetTypeMapping(typeReference);
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