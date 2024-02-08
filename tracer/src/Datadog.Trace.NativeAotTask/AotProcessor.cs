// <copyright file="AotProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Datadog.Trace.NativeAotTask;

internal class AotProcessor
{
    private static Action<string> Log { get; set; }

    private static (string Path, AssemblyDefinition Assembly) ReadAssembly(string path)
    {
        var readerParameters = new ReaderParameters
        {
            ReadingMode = ReadingMode.Immediate,
            ReadWrite = true,
            InMemory = true
        };

        return (path, AssemblyDefinition.ReadAssembly(path, readerParameters));
    }

    internal static void Invoke(IReadOnlyList<string> assemblies, Action<string> progress)
    {
        Log = progress;

        Log("Inside AotProcessor.Invoke");

        var entryPointAssembly = ReadAssembly(assemblies.First());
        var datadogAssembly = ReadAssembly(assemblies.First(a => Path.GetFileName(a) == "Datadog.Trace.dll"));

        entryPointAssembly.Assembly.MainModule.AssemblyReferences.Add(datadogAssembly.Assembly.Name);

        PatchEntryPoint(entryPointAssembly.Assembly, datadogAssembly.Assembly);
        GenerateEntryPointDuckTypes(datadogAssembly.Assembly, entryPointAssembly.Assembly);
        GenerateEntryPointReverseDuckTypes(datadogAssembly.Assembly, entryPointAssembly.Assembly);
        InstrumentMethod(entryPointAssembly.Assembly, datadogAssembly.Assembly);

        Log($"Writing entryPointAssembly to {entryPointAssembly.Path}");
        entryPointAssembly.Assembly.Write(entryPointAssembly.Path);

        Log($"Writing datadogAssembly to {datadogAssembly.Path}");
        datadogAssembly.Assembly.Write(datadogAssembly.Path);
    }

    private static void GenerateEntryPointDuckTypes(AssemblyDefinition datadogAssembly, AssemblyDefinition entryPointAssembly)
    {
        foreach (var duckTypeInterface in datadogAssembly.MainModule.Types)
        {
            var duckTypeAttribute = duckTypeInterface.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "DuckTypeAttribute");

            if (duckTypeAttribute == null)
            {
                continue;
            }

            if (duckTypeAttribute.ConstructorArguments.Count != 2)
            {
                Log($"DuckTypeAttribute on {duckTypeInterface.Name} has invalid number of arguments");
                continue;
            }

            var targetTypeName = duckTypeAttribute.ConstructorArguments[0].Value.ToString();
            var targetAssemblyName = duckTypeAttribute.ConstructorArguments[1].Value.ToString();

            if (targetAssemblyName != entryPointAssembly.Name.Name)
            {
                continue;
            }

            Log($"Injecting {duckTypeInterface.Name} into target {targetTypeName} in {targetAssemblyName}");

            var targetType = entryPointAssembly.MainModule.GetType(targetTypeName);
            var duckTypeInterfaceReference = entryPointAssembly.MainModule.ImportReference(duckTypeInterface);

            targetType.Interfaces.Add(new InterfaceImplementation(duckTypeInterfaceReference));

            foreach (var method in duckTypeInterface.Methods)
            {
                var newMethod = new MethodDefinition($"{duckTypeInterface.FullName}.{method.Name}", default, method.ReturnType)
                {
                    IsNewSlot = true,
                    IsVirtual = true,
                    IsSpecialName = true,
                    IsFinal = true,
                    IsPrivate = true,
                    IsHideBySig = true
                };

                var objectTypeReference = entryPointAssembly.MainModule.ImportReference(typeof(object));

                newMethod.Parameters.Add(new ParameterDefinition(objectTypeReference));

                newMethod.Overrides.Add(entryPointAssembly.MainModule.ImportReference(method));

                var ilProcessor = newMethod.Body.GetILProcessor();

                ilProcessor.Append(Instruction.Create(OpCodes.Ldarg_0));
                ilProcessor.Append(Instruction.Create(OpCodes.Ldarg_1));
                ilProcessor.Append(Instruction.Create(OpCodes.Call, targetType.Methods.First(p => p.Name == method.Name)));
                ilProcessor.Append(Instruction.Create(OpCodes.Ret));

                targetType.Methods.Add(newMethod);
            }
        }

        var duckCopyInterface = datadogAssembly.MainModule.GetType("Datadog.IDuckCopy`1");

        foreach (var duckType in datadogAssembly.MainModule.Types)
        {
            var duckCopyAttribute = duckType.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "DuckCopyAttribute");

            if (duckCopyAttribute == null)
            {
                continue;
            }

            var targetTypeName = duckCopyAttribute.ConstructorArguments[0].Value.ToString();
            var targetAssemblyName = duckCopyAttribute.ConstructorArguments[1].Value.ToString();

            if (targetAssemblyName != entryPointAssembly.Name.Name)
            {
                continue;
            }

            Log($"Implement IDuckCopy into target {targetTypeName} in {targetAssemblyName} to return {duckType.Name} ");

            var targetType = entryPointAssembly.MainModule.GetType(targetTypeName);

            var duckTypeReference = entryPointAssembly.MainModule.ImportReference(duckType);

            var genericDuckCopyInterface = duckCopyInterface.MakeGenericInstanceType(duckTypeReference);
            var duckCopyInterfaceReference = entryPointAssembly.MainModule.ImportReference(genericDuckCopyInterface);

            targetType.Interfaces.Add(new InterfaceImplementation(duckCopyInterfaceReference));

            var newMethod = new MethodDefinition($"Datadog.IDuckCopy<{duckType.FullName}>.DuckCopy", default, duckTypeReference)
            {
                IsNewSlot = true,
                IsVirtual = true,
                IsFinal = true,
                IsPrivate = true,
                IsHideBySig = true
            };

            var duckCopyMethod = duckCopyInterface.Methods.Single();

            var duckCopyMethodGenericReference = new MethodReference(duckCopyMethod.Name, duckCopyMethod.ReturnType, genericDuckCopyInterface)
            {
                CallingConvention = duckCopyMethod.CallingConvention,
                HasThis = duckCopyMethod.HasThis,
                ExplicitThis = duckCopyMethod.ExplicitThis,
            };

            newMethod.Overrides.Add(entryPointAssembly.MainModule.ImportReference(duckCopyMethodGenericReference));
            newMethod.Body.Variables.Add(new VariableDefinition(duckTypeReference));

            var ilProcessor = newMethod.Body.GetILProcessor();

            ilProcessor.Append(Instruction.Create(OpCodes.Ldloca_S, newMethod.Body.Variables[0]));
            ilProcessor.Append(Instruction.Create(OpCodes.Initobj, duckTypeReference));

            foreach (var field in duckType.Fields)
            {
                var targetProperty = targetType.Properties.First(p => p.Name == field.Name);

                ilProcessor.Append(Instruction.Create(OpCodes.Ldloca_S, newMethod.Body.Variables[0]));
                ilProcessor.Append(Instruction.Create(OpCodes.Ldarg_0));
                ilProcessor.Append(Instruction.Create(OpCodes.Call, targetProperty.GetMethod));
                ilProcessor.Append(Instruction.Create(OpCodes.Stfld, entryPointAssembly.MainModule.ImportReference(field)));
            }

            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldloc_0));
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));

            targetType.Methods.Add(newMethod);
        }
    }

    private static void GenerateEntryPointReverseDuckTypes(AssemblyDefinition datadogAssembly, AssemblyDefinition entryPointAssembly)
    {
        foreach (var duckTypeInterface in datadogAssembly.MainModule.Types)
        {
            var duckTypeAttribute = duckTypeInterface.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "ReverseDuckTypeAttribute");

            if (duckTypeAttribute == null)
            {
                continue;
            }

            var targetTypeName = duckTypeAttribute.ConstructorArguments[0].Value.ToString();
            var targetAssemblyName = duckTypeAttribute.ConstructorArguments[1].Value.ToString();

            if (targetAssemblyName != entryPointAssembly.Name.Name)
            {
                continue;
            }

            Log($"Creating proxy {duckTypeInterface.Name} for target {targetTypeName} in {targetAssemblyName}");

            var targetType = entryPointAssembly.MainModule.GetType(targetTypeName);

            var reverseDuckTypeType = new TypeDefinition(
                duckTypeInterface.Namespace,
                "<>Proxy",
                TypeAttributes.AnsiClass | TypeAttributes.AutoLayout | TypeAttributes.BeforeFieldInit | TypeAttributes.NestedPublic,
                datadogAssembly.MainModule.ImportReference(targetType));

            var instanceField = new FieldDefinition("_instance", FieldAttributes.Private, duckTypeInterface);
            reverseDuckTypeType.Fields.Add(instanceField);

            var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, datadogAssembly.MainModule.TypeSystem.Void);
            ctor.Parameters.Add(new ParameterDefinition(duckTypeInterface));

            var ctorIlProcessor = ctor.Body.GetILProcessor();
            ctorIlProcessor.Append(Instruction.Create(OpCodes.Ldarg_0));
            ctorIlProcessor.Append(Instruction.Create(OpCodes.Call, datadogAssembly.MainModule.ImportReference(datadogAssembly.MainModule.TypeSystem.Object.Resolve().GetConstructors().First())));
            ctorIlProcessor.Append(Instruction.Create(OpCodes.Ldarg_0));
            ctorIlProcessor.Append(Instruction.Create(OpCodes.Ldarg_1));
            ctorIlProcessor.Append(Instruction.Create(OpCodes.Stfld, instanceField));
            ctorIlProcessor.Append(Instruction.Create(OpCodes.Ret));

            reverseDuckTypeType.Methods.Add(ctor);

            foreach (var method in duckTypeInterface.Methods)
            {
                if (!method.CustomAttributes.Any(a => a.AttributeType.Name == "DuckReverseMethodAttribute"))
                {
                    continue;
                }

                var methodToOverride = datadogAssembly.MainModule.ImportReference(targetType.Methods.First(p => p.Name == method.Name));

                var newMethod = new MethodDefinition($"{method.Name}", method.Attributes, method.ReturnType)
                {
                    IsVirtual = true,
                    IsHideBySig = true
                };

                foreach (var parameter in method.Parameters)
                {
                    newMethod.Parameters.Add(new ParameterDefinition(entryPointAssembly.MainModule.ImportReference(parameter.ParameterType)));
                }

                newMethod.Overrides.Add(methodToOverride);

                var ilProcessor = newMethod.Body.GetILProcessor();

                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    ilProcessor.Append(Instruction.Create(OpCodes.Ldarg, i + 1));
                }

                ilProcessor.Append(Instruction.Create(OpCodes.Ldarg_0));
                ilProcessor.Append(Instruction.Create(OpCodes.Ldfld, instanceField));
                ilProcessor.Append(Instruction.Create(OpCodes.Callvirt, method));

                ilProcessor.Append(Instruction.Create(OpCodes.Ret));

                reverseDuckTypeType.Methods.Add(newMethod);
            }

            duckTypeInterface.NestedTypes.Add(reverseDuckTypeType);

            // Implement IReverseDuckType
            var reverseDuckTypeInterface = datadogAssembly.MainModule.GetType("Datadog.IReverseDuckType");

            var createProxyMethod = new MethodDefinition("CreateProxy", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual, datadogAssembly.MainModule.TypeSystem.Object);
            createProxyMethod.Parameters.Add(new ParameterDefinition(datadogAssembly.MainModule.ImportReference(typeof(Type))));

            var createProxyIlProcessor = createProxyMethod.Body.GetILProcessor();

            // In the real case, we would check the type and branch to the right constructor
            createProxyIlProcessor.Append(createProxyIlProcessor.Create(OpCodes.Ldarg_0));
            createProxyIlProcessor.Append(createProxyIlProcessor.Create(OpCodes.Newobj, ctor));
            createProxyIlProcessor.Append(createProxyIlProcessor.Create(OpCodes.Ret));

            duckTypeInterface.Methods.Add(createProxyMethod);

            duckTypeInterface.Interfaces.Add(new InterfaceImplementation(reverseDuckTypeInterface));
        }
    }

    private static void InstrumentMethod(AssemblyDefinition entryPointAssembly, AssemblyDefinition datadogAssembly)
    {
        var classToInstrument = entryPointAssembly.MainModule.GetType("TargetApplication.ClassToInstrument");
        var methodToInstrument = classToInstrument.Methods.First(m => m.Name == "MethodToInstrument");

        var ilProcessor = methodToInstrument.Body.GetILProcessor();

        var instrumentationType = datadogAssembly.MainModule.GetType("Datadog.Instrumentation");
        var invokeMethod = instrumentationType.Methods.First(m => m.Name == "OnMethodToInstrument");

        var invokeMethodRef = entryPointAssembly.MainModule.ImportReference(invokeMethod);

        var start = ilProcessor.Body.Instructions[0];

        ilProcessor.InsertBefore(start, Instruction.Create(OpCodes.Ldarg_0));
        ilProcessor.InsertBefore(start, Instruction.Create(OpCodes.Call, invokeMethodRef));
    }

    private static void PatchEntryPoint(AssemblyDefinition entryPointAssembly, AssemblyDefinition datadogAssembly)
    {
        var ilProcessor = entryPointAssembly.EntryPoint.Body.GetILProcessor();

        var agentType = datadogAssembly.MainModule.GetType("Datadog.Trace.ClrProfiler.Instrumentation");
        var runMethod = agentType.Methods.First(m => m.Name == "InitializeNoNativeParts");

        var runMethodRef = entryPointAssembly.MainModule.ImportReference(runMethod);

        var start = ilProcessor.Body.Instructions[0];

        ilProcessor.InsertBefore(start, Instruction.Create(OpCodes.Ldnull));
        ilProcessor.InsertBefore(start, Instruction.Create(OpCodes.Call, runMethodRef));
    }
}
