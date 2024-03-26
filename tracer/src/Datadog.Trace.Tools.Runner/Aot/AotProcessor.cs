// <copyright file="AotProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.Aot
{
#if NET6_0_OR_GREATER
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "System.Reflection.Assembly.GetType is only called over the tracer assembly.")]
#endif
    internal class AotProcessor
    {
        private static readonly NativeCallTargetDefinition[] Definitions;
        private static readonly NativeCallTargetDefinition[] DerivedDefinitions;

        private static readonly Assembly TracerAssembly;

        private static readonly MethodInfo[] CallTargetInvokerMethods;
        private static readonly MethodInfo LogExceptionMethodInfo;
        private static readonly MethodInfo GetDefaultValueMethodInfo;

        private static readonly MethodInfo CallTargetStateGetDefaultMethodInfo;

#if NET6_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        private static readonly Type CallTargetReturnVoid;
#else
        private static readonly Type CallTargetReturnVoid;
#endif
        private static readonly MethodInfo CallTargetReturnVoidGetDefaultValueMethodInfo;

#if NET6_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        private static readonly Type CallTargetReturn;
#else
        private static readonly Type CallTargetReturn;
#endif
        private static readonly MethodInfo CallTargetReturnGetReturnValueMethodInfo;
        private static readonly MethodInfo CallTargetReturnGetDefaultValueMethodInfo;

        static AotProcessor()
        {
            Definitions = InstrumentationDefinitions.GetAllDefinitionsNative();
            DerivedDefinitions = InstrumentationDefinitions.GetAllDerivedDefinitionsNative();
            TracerAssembly = typeof(Instrumentation).Assembly;

            var callTargetInvokerType = typeof(CallTargetInvoker);
            CallTargetInvokerMethods = callTargetInvokerType.GetMethods();
            LogExceptionMethodInfo = CallTargetInvokerMethods.FirstOrDefault(m => m.Name == "LogException");
            GetDefaultValueMethodInfo = CallTargetInvokerMethods.FirstOrDefault(m => m.Name == "GetDefaultValue");

            var callTargetStateType = typeof(CallTargetState);
            CallTargetStateGetDefaultMethodInfo = callTargetStateType.GetMethod("GetDefault", BindingFlags.Public | BindingFlags.Static);

            CallTargetReturnVoid = typeof(CallTargetReturn);
            var callTargetReturnVoidMethods = CallTargetReturnVoid.GetMethods();
            CallTargetReturnVoidGetDefaultValueMethodInfo = callTargetReturnVoidMethods.FirstOrDefault(m => m.Name == "GetDefault");

            CallTargetReturn = typeof(CallTargetReturn<>);
            var callTargetReturnMethods = CallTargetReturn.GetMethods();
            CallTargetReturnGetReturnValueMethodInfo = callTargetReturnMethods.FirstOrDefault(m => m.Name == "GetReturnValue");
            CallTargetReturnGetDefaultValueMethodInfo = callTargetReturnMethods.FirstOrDefault(m => m.Name == "GetDefault");
        }

        public static void ProcessFolder(string inputFolder, string outputFolder)
        {
            if (!Directory.Exists(inputFolder))
            {
                throw new DirectoryNotFoundException("Input folder doesn't exist.");
            }

            if (!Directory.Exists(outputFolder))
            {
                throw new DirectoryNotFoundException("Output folder doesn't exist.");
            }

            int processed = 0;
            Parallel.ForEach(Directory.EnumerateFiles(inputFolder, "*.*", SearchOption.TopDirectoryOnly), file =>
            {
                if (Path.GetExtension(file).ToLowerInvariant() != ".dll")
                {
                    File.Copy(file, Path.Combine(outputFolder, Path.GetFileName(file)), true);
                }
                else if (TryProcessAssembly(file, Path.Combine(outputFolder, Path.GetFileName(file))))
                {
                    Interlocked.Increment(ref processed);
                }
            });

            if (processed > 0)
            {
                var tracerAssembly = typeof(Datadog.Trace.Tracer).Assembly.Location;
                File.Copy(tracerAssembly, Path.Combine(outputFolder, Path.GetFileName(Path.GetFileName(tracerAssembly))), true);

                AnsiConsole.WriteLine("Patching deps.json file");

                var version = typeof(Instrumentation).Assembly.GetName().Version?.ToString();
                foreach (var depsJsonPath in Directory.EnumerateFiles(outputFolder, "*.deps.json", SearchOption.TopDirectoryOnly))
                {
                    var json = JObject.Parse(File.ReadAllText(depsJsonPath));
                    var libraries = (JObject)json["libraries"];
                    libraries.Add($"Datadog.Trace/{version}", JObject.FromObject(new
                    {
                        type = "reference",
                        serviceable = false,
                        sha512 = string.Empty
                    }));

                    var targets = (JObject)json["targets"];
                    foreach (var targetProperty in targets.Properties())
                    {
                        var target = (JObject)targetProperty.Value;

                        target.Add($"Datadog.Trace/{version}", new JObject(new JProperty("runtime", new JObject(
                                new JProperty("Datadog.Trace.dll", new JObject(
                                    new JProperty("assemblyVersion", version),
                                    new JProperty("fileVersion", version)))))));
                    }

                    using (var stream = File.CreateText(depsJsonPath))
                    {
                        using (var writer = new JsonTextWriter(stream) { Formatting = Formatting.Indented })
                        {
                            json.WriteTo(writer);
                        }
                    }
                }

                AnsiConsole.WriteLine("Done");
            }

            AnsiConsole.WriteLine($"{processed} files processed.");
        }

        private static bool TryProcessAssembly(string inputPath, string outputPath)
        {
            bool processed = false;
            try
            {
                var asmResolver = new DefaultAssemblyResolver();
                foreach (var path in asmResolver.GetSearchDirectories())
                {
                    asmResolver.RemoveSearchDirectory(path);
                }

                asmResolver.AddSearchDirectory(Path.GetDirectoryName(inputPath));
                using (var assemblyDefinition = AssemblyDefinition.ReadAssembly(inputPath, new ReaderParameters { AssemblyResolver = asmResolver }))
                {
                    var lstDefinitionsDefs = new List<DefinitionItem>();
                    var moduleDefinition = assemblyDefinition.MainModule;

                    // Extract direct definitions
                    foreach (var definition in Definitions)
                    {
                        if (Marshal.PtrToStringUni(definition.TargetAssembly) != assemblyDefinition.Name.Name)
                        {
                            continue;
                        }

                        if (assemblyDefinition.Name.Version is not null)
                        {
                            var minVersion = new Version(definition.TargetMinimumMajor, definition.TargetMinimumMinor, definition.TargetMinimumPatch);
                            var maxVersion = new Version(definition.TargetMaximumMajor, definition.TargetMaximumMinor, definition.TargetMaximumPatch);

                            if (assemblyDefinition.Name.Version < minVersion)
                            {
                                continue;
                            }

                            if (assemblyDefinition.Name.Version > maxVersion)
                            {
                                continue;
                            }
                        }

                        var typeDefinition = moduleDefinition.Types.FirstOrDefault(t => t.FullName == Marshal.PtrToStringUni(definition.TargetType));
                        if (typeDefinition is null && moduleDefinition.HasExportedTypes)
                        {
                            var exportedType = moduleDefinition.ExportedTypes.FirstOrDefault(eType => eType.FullName == Marshal.PtrToStringUni(definition.TargetType));
                            if (exportedType is not null)
                            {
                                try
                                {
                                    typeDefinition = exportedType.Resolve();
                                }
                                catch
                                {
                                    // ...
                                }
                            }
                        }

                        if (typeDefinition is not null)
                        {
                            RetrieveMethodsInTypeDefinition(typeDefinition, definition, lstDefinitionsDefs, assemblyDefinition);
                        }
                    }

                    // Extract derived definitions
                    var assemblyReferences = moduleDefinition.AssemblyReferences;
                    foreach (var assemblyReference in assemblyReferences)
                    {
                        foreach (var definition in DerivedDefinitions)
                        {
                            if (Marshal.PtrToStringUni(definition.TargetAssembly) != assemblyReference.Name)
                            {
                                continue;
                            }

                            var minVersion = new Version(definition.TargetMinimumMajor, definition.TargetMinimumMinor, definition.TargetMinimumPatch);
                            var maxVersion = new Version(definition.TargetMaximumMajor, definition.TargetMaximumMinor, definition.TargetMaximumPatch);

                            if (assemblyReference.Version < minVersion)
                            {
                                continue;
                            }

                            if (assemblyReference.Version > maxVersion)
                            {
                                continue;
                            }

                            var asmName = moduleDefinition.Assembly.FullName;

                            foreach (var typeDefinition in moduleDefinition.Types)
                            {
                                var baseTypeReference = typeDefinition.BaseType;
                                if (baseTypeReference != null && baseTypeReference.FullName == Marshal.PtrToStringUni(definition.TargetType))
                                {
                                    RetrieveMethodsInTypeDefinition(typeDefinition, definition, lstDefinitionsDefs, assemblyDefinition);
                                }
                            }
                        }
                    }

                    if (lstDefinitionsDefs.Count == 0)
                    {
                        return false;
                    }

                    AnsiConsole.WriteLine($"{assemblyDefinition.Name.FullName} => {lstDefinitionsDefs.Count}");
                    if (ProcessDefinitions(moduleDefinition, lstDefinitionsDefs))
                    {
                        if ((moduleDefinition.Attributes & ModuleAttributes.ILLibrary) == ModuleAttributes.ILLibrary)
                        {
                            moduleDefinition.Architecture = TargetArchitecture.I386;
                            moduleDefinition.Attributes &= ~ModuleAttributes.ILLibrary;
                            moduleDefinition.Attributes |= ModuleAttributes.ILOnly;
                        }

                        assemblyDefinition.Write(outputPath);
                        processed = true;
                    }
                }
            }
            catch (BadImageFormatException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Utils.WriteError($"{inputPath}: {ex.Message}");
                return false;
            }
            finally
            {
                if (!processed)
                {
                    File.Copy(inputPath, outputPath, true);
                }
            }

            return true;

            void RetrieveMethodsInTypeDefinition(TypeDefinition typeDefinition, NativeCallTargetDefinition definition, List<DefinitionItem> lstDefinitionsDefs, AssemblyDefinition assemblyDefinition)
            {
                foreach (var mDefinition in typeDefinition.Methods.Where(m => m.Name == Marshal.PtrToStringUni(definition.TargetMethod)))
                {
                    var lstParameters = mDefinition.Parameters;
                    if (lstParameters.Count != definition.TargetSignatureTypesLength - 1)
                    {
                        continue;
                    }

                    bool parameters = true;
                    var ptr = definition.TargetSignatureTypes;
                    for (var i = 0; i < definition.TargetSignatureTypesLength; i++)
                    {
                        var localPtr = Marshal.ReadIntPtr(ptr);
                        var localString = Marshal.PtrToStringUni(localPtr);
                        ptr += Marshal.SizeOf<IntPtr>();

                        if (localString == "_")
                        {
                            continue;
                        }

                        if (i == 0)
                        {
                            if (mDefinition.ReturnType.FullName != localString)
                            {
                                parameters = false;
                                break;
                            }
                        }
                        else if (lstParameters[i - 1].ParameterType.FullName != localString)
                        {
                            parameters = false;
                            break;
                        }
                    }

                    if (parameters)
                    {
                        var methodDefinition = mDefinition;
                        var integrationType = TracerAssembly.GetType(Marshal.PtrToStringUni(definition.IntegrationType), false);
                        if (integrationType is not null)
                        {
                            lstDefinitionsDefs.Add(new DefinitionItem(assemblyDefinition, typeDefinition, methodDefinition, integrationType, definition));
                        }

                        break;
                    }
                }
            }
        }

        private static bool ProcessDefinitions(ModuleDefinition moduleDefinition, List<DefinitionItem> definitions)
        {
            var typeReferenceCache = new Dictionary<Type, TypeReference>();
            var methodReferenceCache = new Dictionary<MethodInfo, MethodReference>();

            var exceptionTypeReference = new TypeReference(typeof(Exception).Namespace, nameof(Exception), moduleDefinition, moduleDefinition.TypeSystem.CoreLibrary);

            foreach (var definition in definitions)
            {
                var methodDefinition = definition.TargetMethodDefinition;
                if (!methodDefinition.HasBody)
                {
                    continue;
                }

                var integrationTypeReference = ImportTypeReference(definition.IntegrationType);
                var targetTypeDefinition = definition.TargetTypeDefinition;
                var methodReturnTypeReference = methodDefinition.ReturnType;

                var lstParameters = definition.TargetMethodDefinition.Parameters;

                // CallTargetReturn
                var isVoidReturn = definition.TargetMethodDefinition.ReturnType == moduleDefinition.TypeSystem.Void;
                var callTargetReturnType = isVoidReturn ? CallTargetReturnVoid : CallTargetReturn;

                var callTargetReturnTypeReference = ImportTypeReference(callTargetReturnType);
                GenericInstanceType callTargetReturnTypeGenericInstance = null;
                MethodReference getReturnValueMethodReference = null;
                MethodReference getDefaultValueReturnTypeMethodReference;
                if (!isVoidReturn)
                {
                    callTargetReturnTypeGenericInstance = new GenericInstanceType(callTargetReturnTypeReference);
                    callTargetReturnTypeGenericInstance.GenericArguments.Add(methodReturnTypeReference);

                    getReturnValueMethodReference = ImportMethodReference(CallTargetReturnGetReturnValueMethodInfo);
                    getReturnValueMethodReference.DeclaringType = callTargetReturnTypeGenericInstance;

                    getDefaultValueReturnTypeMethodReference = ImportMethodReference(CallTargetReturnGetDefaultValueMethodInfo);
                    getDefaultValueReturnTypeMethodReference.DeclaringType = callTargetReturnTypeGenericInstance;
                }
                else
                {
                    getDefaultValueReturnTypeMethodReference = ImportMethodReference(CallTargetReturnVoidGetDefaultValueMethodInfo);
                }

                // BeginMethod
                var beginMethodMethodInfo = CallTargetInvokerMethods.FirstOrDefault(m =>
                {
                    if (m.Name != "BeginMethod")
                    {
                        return false;
                    }

                    var parameters = m.GetParameters();

                    if (parameters.Length != lstParameters.Count + 1)
                    {
                        return false;
                    }

                    if (parameters.Length > 1 && !parameters[parameters.Length - 1].ParameterType.IsByRef)
                    {
                        return false;
                    }

                    return true;
                });
                var beginMethodMethodReference = ImportMethodReference(beginMethodMethodInfo);
                var beginMethodMethodSpec = new GenericInstanceMethod(beginMethodMethodReference);
                beginMethodMethodSpec.GenericArguments.Add(integrationTypeReference);
                beginMethodMethodSpec.GenericArguments.Add(targetTypeDefinition);
                foreach (var parameterDefinition in lstParameters)
                {
                    beginMethodMethodSpec.GenericArguments.Add(parameterDefinition.ParameterType);
                }

                var callTargetStateTypeReference = beginMethodMethodSpec.ReturnType;
                var callTargetStateGetDefaultMethodReference = ImportMethodReference(CallTargetStateGetDefaultMethodInfo);

                // EndMethod
                var endMethodMethodInfo = CallTargetInvokerMethods.FirstOrDefault(m =>
                {
                    if (m.Name != "EndMethod")
                    {
                        return false;
                    }

                    if (!isVoidReturn && m.ReturnType == typeof(CallTargetReturn))
                    {
                        return false;
                    }

                    if (!m.GetParameters().Last().ParameterType.IsByRef)
                    {
                        return false;
                    }

                    return true;
                });
                var endMethodMethodReference = ImportMethodReference(endMethodMethodInfo);
                var endMethodMethodSpec = new GenericInstanceMethod(endMethodMethodReference);
                endMethodMethodSpec.GenericArguments.Add(integrationTypeReference);
                endMethodMethodSpec.GenericArguments.Add(targetTypeDefinition);
                if (!isVoidReturn)
                {
                    endMethodMethodSpec.GenericArguments.Add(methodReturnTypeReference);
                }

                // LogException
                var logExceptionMethodReference = ImportMethodReference(LogExceptionMethodInfo);
                var logExceptionMethodSpec = new GenericInstanceMethod(logExceptionMethodReference);
                logExceptionMethodSpec.GenericArguments.Add(integrationTypeReference);
                logExceptionMethodSpec.GenericArguments.Add(targetTypeDefinition);

                // GetDefaultValue
                var getDefaultValueMethodReference = ImportMethodReference(GetDefaultValueMethodInfo);
                var getDefaultValueMethodSpec = new GenericInstanceMethod(getDefaultValueMethodReference);
                getDefaultValueMethodSpec.GenericArguments.Add(methodReturnTypeReference);

                // ***
                // Locals
                // ***

                // Add new locals Add locals for TReturn (if non-void method), CallTargetState, CallTargetReturn/CallTargetReturn<TReturn>, Exception
                var methodBody = methodDefinition.Body;

                VariableDefinition returnValueLocal = null;
                VariableDefinition callTargetReturnLocal = null;
                if (!isVoidReturn)
                {
                    returnValueLocal = new VariableDefinition(methodReturnTypeReference);
                    callTargetReturnLocal = new VariableDefinition(callTargetReturnTypeGenericInstance);
                    methodBody.Variables.Add(returnValueLocal);
                    methodBody.Variables.Add(callTargetReturnLocal);
                }
                else
                {
                    callTargetReturnLocal = new VariableDefinition(callTargetReturnTypeReference);
                    methodBody.Variables.Add(callTargetReturnLocal);
                }

                var callTargetStateLocal = new VariableDefinition(callTargetStateTypeReference);
                var exceptionLocal = new VariableDefinition(exceptionTypeReference);
                methodBody.Variables.Add(callTargetStateLocal);
                methodBody.Variables.Add(exceptionLocal);

                // ***
                // IL Rewriting
                // ***
                var beginOriginalMethodInstruction = methodBody.Instructions.First();
                var index = 0;

                // Initialize
                if (returnValueLocal is not null)
                {
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, getDefaultValueMethodSpec));
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc, returnValueLocal));
                }

                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, callTargetStateGetDefaultMethodReference));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc, callTargetStateLocal));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, getDefaultValueReturnTypeMethodReference));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc, callTargetReturnLocal));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldnull));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc, exceptionLocal));

                // BeginMethod
                Instruction firstInstruction;
                if (methodDefinition.IsStatic)
                {
                    if (definition.TargetTypeDefinition.IsValueType)
                    {
                        return false;
                    }

                    firstInstruction = Instruction.Create(OpCodes.Ldnull);
                    methodBody.Instructions.Insert(index++, firstInstruction);
                }
                else
                {
                    firstInstruction = Instruction.Create(OpCodes.Ldarg_0);
                    methodBody.Instructions.Insert(index++, firstInstruction);
                    if (definition.TargetTypeDefinition.IsValueType)
                    {
                        if (definition.TargetTypeDefinition.HasGenericParameters)
                        {
                            return false;
                        }

                        methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldobj, definition.TargetTypeDefinition));
                    }
                }

                foreach (var mParameter in lstParameters)
                {
                    if (mParameter.ParameterType.IsByReference)
                    {
                        methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldarg, mParameter));
                    }
                    else
                    {
                        methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldarga, mParameter));
                    }
                }

                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, beginMethodMethodSpec));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc, callTargetStateLocal));

                var stateLeaveToBeginOriginalMethodInstruction = Instruction.Create(OpCodes.Leave, beginOriginalMethodInstruction);
                methodBody.Instructions.Insert(index++, stateLeaveToBeginOriginalMethodInstruction);

                // *** BeginMethod call catch
                var beginMethodCatchFirstInstruction = Instruction.Create(OpCodes.Call, logExceptionMethodSpec);
                methodBody.Instructions.Insert(index++, beginMethodCatchFirstInstruction);

                var beginMethodCatchLeaveInstruction = Instruction.Create(OpCodes.Leave, beginOriginalMethodInstruction);
                methodBody.Instructions.Insert(index++, beginMethodCatchLeaveInstruction);

                // *** BeginMethod exception handling clause
                var beginMethodExClause = new ExceptionHandler(ExceptionHandlerType.Catch)
                {
                    TryStart = firstInstruction,
                    TryEnd = beginMethodCatchFirstInstruction,
                    HandlerStart = beginMethodCatchFirstInstruction,
                    HandlerEnd = beginMethodCatchLeaveInstruction.Next,
                    CatchType = exceptionTypeReference
                };
                methodBody.ExceptionHandlers.Add(beginMethodExClause);

                // ***
                // ENDING OF THE METHOD EXECUTION
                // ***

                // *** Create return instruction and insert it at the end
                var methodReturnInstruction = Instruction.Create(OpCodes.Ret);
                methodBody.Instructions.Add(methodReturnInstruction);
                index = methodBody.Instructions.IndexOf(methodReturnInstruction);

                // ***
                // EXCEPTION CATCH
                // ***
                var startExceptionCatch = Instruction.Create(OpCodes.Stloc, exceptionLocal);
                methodBody.Instructions.Insert(index++, startExceptionCatch);
                var rethrowInstruction = Instruction.Create(OpCodes.Rethrow);
                methodBody.Instructions.Insert(index++, rethrowInstruction);

                // ***
                // EXCEPTION FINALLY / END METHOD PART
                // ***
                Instruction endMethodTryStartInstruction;
                if (methodDefinition.IsStatic)
                {
                    if (definition.TargetTypeDefinition.IsValueType)
                    {
                        return false;
                    }

                    endMethodTryStartInstruction = Instruction.Create(OpCodes.Ldnull);
                    methodBody.Instructions.Insert(index++, endMethodTryStartInstruction);
                }
                else
                {
                    endMethodTryStartInstruction = Instruction.Create(OpCodes.Ldarg_0);
                    methodBody.Instructions.Insert(index++, endMethodTryStartInstruction);
                    if (definition.TargetTypeDefinition.IsValueType)
                    {
                        if (definition.TargetTypeDefinition.HasGenericParameters)
                        {
                            return false;
                        }

                        methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldobj, definition.TargetTypeDefinition));
                    }
                }

                // *** Load the return value is is not void
                if (returnValueLocal is not null)
                {
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloc, returnValueLocal));
                }

                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloc, exceptionLocal));
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloca, callTargetStateLocal));
                var endMethodCallInstruction = Instruction.Create(OpCodes.Call, endMethodMethodSpec);
                methodBody.Instructions.Insert(index++, endMethodCallInstruction);
                methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc, callTargetReturnLocal));

                if (returnValueLocal is not null)
                {
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloca, callTargetReturnLocal));
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Call, getReturnValueMethodReference));
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Stloc, returnValueLocal));
                }

                var endMethodTryLeaveInstruction = Instruction.Create(OpCodes.Nop);
                endMethodTryLeaveInstruction.OpCode = OpCodes.Leave;
                methodBody.Instructions.Insert(index++, endMethodTryLeaveInstruction);

                // *** EndMethod call catch
                var endMethodCatchFirstInstruction = Instruction.Create(OpCodes.Call, logExceptionMethodSpec);
                methodBody.Instructions.Insert(index++, endMethodCatchFirstInstruction);
                var endMethodCatchLeaveInstruction = Instruction.Create(OpCodes.Nop);
                endMethodCatchLeaveInstruction.OpCode = OpCodes.Leave;
                methodBody.Instructions.Insert(index++, endMethodCatchLeaveInstruction);

                // *** EndMethod leave to finally
                var endFinallyInstruction = Instruction.Create(OpCodes.Endfinally);
                methodBody.Instructions.Insert(index++, endFinallyInstruction);
                endMethodTryLeaveInstruction.Operand = endFinallyInstruction;
                endMethodCatchLeaveInstruction.Operand = endFinallyInstruction;

                // *** EndMethod exception handling clause
                var endMethodExClause = new ExceptionHandler(ExceptionHandlerType.Catch)
                {
                    TryStart = endMethodTryStartInstruction,
                    TryEnd = endMethodCatchFirstInstruction,
                    HandlerStart = endMethodCatchFirstInstruction,
                    HandlerEnd = endMethodCatchLeaveInstruction.Next,
                    CatchType = exceptionTypeReference
                };
                methodBody.ExceptionHandlers.Add(endMethodExClause);

                // ***
                // METHOD RETURN
                // ***

                // Load the current return value from the local var
                if (returnValueLocal is not null)
                {
                    methodBody.Instructions.Insert(index++, Instruction.Create(OpCodes.Ldloc, returnValueLocal));
                }

                // Changes all returns to a LEAVE
                for (var i = 0; i < methodBody.Instructions.Count; i++)
                {
                    var instr = methodBody.Instructions[i];
                    if (instr.OpCode == OpCodes.Ret)
                    {
                        if (instr != methodReturnInstruction)
                        {
                            if (returnValueLocal is not null)
                            {
                                methodBody.Instructions.Insert(i, Instruction.Create(OpCodes.Stloc, returnValueLocal));
                            }

                            instr.OpCode = OpCodes.Leave;
                            instr.Operand = endFinallyInstruction.Next;
                        }
                    }
                }

                // Exception handling clauses
                var exClause = new ExceptionHandler(ExceptionHandlerType.Catch)
                {
                    TryStart = firstInstruction,
                    TryEnd = startExceptionCatch,
                    HandlerStart = startExceptionCatch,
                    HandlerEnd = rethrowInstruction.Next,
                    CatchType = exceptionTypeReference
                };
                methodBody.ExceptionHandlers.Add(exClause);

                var finallyClause = new ExceptionHandler(ExceptionHandlerType.Finally)
                {
                    TryStart = firstInstruction,
                    TryEnd = rethrowInstruction.Next,
                    HandlerStart = rethrowInstruction.Next,
                    HandlerEnd = endFinallyInstruction.Next
                };
                methodBody.ExceptionHandlers.Add(finallyClause);

                methodBody.Optimize();
            }

            return true;

            TypeReference ImportTypeReference(Type type)
            {
                if (!typeReferenceCache.TryGetValue(type, out var typeReference))
                {
                    typeReference = moduleDefinition.ImportReference(type);
                    typeReferenceCache[type] = typeReference;
                }

                return typeReference;
            }

            MethodReference ImportMethodReference(MethodInfo methodInfo)
            {
                if (!methodReferenceCache.TryGetValue(methodInfo, out var methodReference))
                {
                    methodReference = moduleDefinition.ImportReference(methodInfo);
                    methodReferenceCache[methodInfo] = methodReference;
                }

                return methodReference;
            }
        }
    }
}
