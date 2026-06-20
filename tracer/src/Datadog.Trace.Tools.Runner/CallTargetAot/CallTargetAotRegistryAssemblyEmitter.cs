// <copyright file="CallTargetAotRegistryAssemblyEmitter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.ClrProfiler.CallTarget;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Datadog.Trace.Tools.Runner.CallTargetAot;

/// <summary>
/// Emits the generated registry assembly used by the CallTarget NativeAOT generation flow.
/// </summary>
internal static class CallTargetAotRegistryAssemblyEmitter
{
    /// <summary>
    /// Defines the generated bootstrap namespace used by the registry.
    /// </summary>
    internal const string BootstrapNamespace = "Datadog.Trace.ClrProfiler.CallTarget.Generated";

    /// <summary>
    /// Defines the generated bootstrap type name used by the registry.
    /// </summary>
    internal const string BootstrapTypeName = "CallTargetAotRegistryBootstrap";

    /// <summary>
    /// Defines the public bootstrap method name consumed by the rewrite step.
    /// </summary>
    internal const string BootstrapMethodName = "Initialize";

    /// <summary>
    /// Defines the synthetic proof marker emitted by the rewritten module initializer after the bootstrap succeeds.
    /// </summary>
    internal const string BootstrapMarker = "CALLTARGET_AOT_BOOTSTRAP:1";

    /// <summary>
    /// Emits the registry assembly and returns its resolved assembly name.
    /// </summary>
    /// <param name="artifactPaths">The artifact paths that determine where the registry assembly is written.</param>
    /// <param name="manifest">The manifest that describes the selected CallTarget matches.</param>
    /// <param name="requestedAssemblyName">The optional assembly name requested by the caller.</param>
    /// <returns>The generated registry assembly name.</returns>
    internal static string Emit(
        CallTargetAotArtifactPaths artifactPaths,
        CallTargetAotManifest manifest,
        string? requestedAssemblyName,
        CallTargetAotDuckTypeGenerationResult? duckTypeGenerationResult = null)
    {
        var outputDirectory = Path.GetDirectoryName(artifactPaths.OutputAssemblyPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory!);
        }

        var assemblyName = string.IsNullOrWhiteSpace(requestedAssemblyName)
                               ? Path.GetFileNameWithoutExtension(artifactPaths.OutputAssemblyPath) ?? "Datadog.Trace.CallTarget.AotRegistry"
                               : requestedAssemblyName!.Trim();

        var assemblyDefinition = AssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition(assemblyName, new Version(1, 0, 0, 0)),
            assemblyName,
            ModuleKind.Dll);
        var module = assemblyDefinition.MainModule;
        var resolver = CreateResolver(manifest, duckTypeGenerationResult);
        var tracerAssembly = AssemblyDefinition.ReadAssembly(manifest.TracerAssemblyPath, new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = false });
        var targetAssemblies = new Dictionary<string, AssemblyDefinition>(StringComparer.OrdinalIgnoreCase);
        AssemblyDefinition? duckTypeAssembly = null;
        try
        {
            CallTargetAotDuckTypeEmitterContext? duckTypeContext = null;
            if (duckTypeGenerationResult is not null)
            {
                duckTypeAssembly = AssemblyDefinition.ReadAssembly(duckTypeGenerationResult.Dependency.RegistryAssemblyPath, new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = false });
                duckTypeContext = CreateDuckTypeContext(module, duckTypeAssembly.MainModule, duckTypeGenerationResult);
            }

            var bootstrapType = new TypeDefinition(
                BootstrapNamespace,
                BootstrapTypeName,
                Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed,
                module.TypeSystem.Object);
            module.Types.Add(bootstrapType);

            var generatedAdapterMethods = EmitAdapterMethods(module, bootstrapType, tracerAssembly.MainModule, manifest.MatchedDefinitions, targetAssemblies, duckTypeContext);
            var typedAsyncRootMethod = EmitTypedAsyncRootMethod(module, bootstrapType, generatedAdapterMethods.Values);
            EmitInitializeMethod(module, bootstrapType, tracerAssembly.MainModule, targetAssemblies.Values.First().MainModule, manifest.MatchedDefinitions, generatedAdapterMethods, duckTypeContext);
            AppendTypedAsyncRootBranch(module, bootstrapType, typedAsyncRootMethod);
            RetargetCoreLibraryReferences(module, bootstrapType, targetAssemblies.Values);
            assemblyDefinition.Write(artifactPaths.OutputAssemblyPath);
            return assemblyName;
        }
        finally
        {
            tracerAssembly.Dispose();
            duckTypeAssembly?.Dispose();
            foreach (var targetAssembly in targetAssemblies.Values)
            {
                targetAssembly.Dispose();
            }

            resolver.Dispose();
        }
    }

    /// <summary>
    /// Emits the generated adapter methods for the matched definitions and returns them by integration binding.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="bootstrapType">The generated bootstrap type that will own the adapters.</param>
    /// <param name="tracerModule">The tracer module that contains the integration types.</param>
    /// <param name="matchedDefinitions">The matched target methods selected for generation.</param>
    /// <param name="targetAssemblies">The cache of opened target assemblies keyed by absolute path.</param>
    /// <returns>The emitted adapter methods keyed by matched definition.</returns>
    private static Dictionary<CallTargetAotMatchedDefinition, GeneratedAdapterSet> EmitAdapterMethods(
        ModuleDefinition module,
        TypeDefinition bootstrapType,
        ModuleDefinition tracerModule,
        IReadOnlyList<CallTargetAotMatchedDefinition> matchedDefinitions,
        IDictionary<string, AssemblyDefinition> targetAssemblies,
        CallTargetAotDuckTypeEmitterContext? duckTypeContext)
    {
        var emittedMethods = new Dictionary<CallTargetAotMatchedDefinition, GeneratedAdapterSet>();
        var beginMethodCache = new Dictionary<string, MethodDefinition>(StringComparer.Ordinal);
        var endMethodCache = new Dictionary<string, MethodDefinition>(StringComparer.Ordinal);
        var asyncMethodCache = new Dictionary<string, GeneratedAsyncAdapter>(StringComparer.Ordinal);
        foreach (var match in matchedDefinitions)
        {
            if (!targetAssemblies.TryGetValue(match.TargetAssemblyPath, out var targetAssembly))
            {
                targetAssembly = AssemblyDefinition.ReadAssembly(match.TargetAssemblyPath, new ReaderParameters { ReadSymbols = false });
                targetAssemblies[match.TargetAssemblyPath] = targetAssembly;
            }

            var targetType = ResolveType(targetAssembly.MainModule, match.TargetTypeName)
                             ?? throw new InvalidOperationException($"The matched target type '{match.TargetTypeName}' could not be resolved from '{match.TargetAssemblyPath}'.");
            var integrationType = ResolveType(tracerModule, match.IntegrationTypeName)
                                  ?? throw new InvalidOperationException($"The integration type '{match.IntegrationTypeName}' could not be resolved from '{tracerModule.FileName}'.");
            var targetMethod = ResolveMatchedMethod(targetType, match)
                               ?? throw new InvalidOperationException($"The matched target method '{match.TargetTypeName}.{match.TargetMethodName}' could not be resolved from '{match.TargetAssemblyPath}'.");
            var duckBindings = ResolveDuckTypeBindings(module, match, duckTypeContext);
            var beginCacheKey = $"{match.IntegrationTypeName}|{match.TargetTypeName}|begin|{string.Join("|", match.ParameterTypeNames)}";
            if (!beginMethodCache.TryGetValue(beginCacheKey, out var beginMethod))
            {
                beginMethod = EmitBeginMethod(module, bootstrapType, integrationType, targetType, targetMethod, match, duckBindings);
                beginMethodCache[beginCacheKey] = beginMethod;
            }

            var asyncAdapter = default(GeneratedAsyncAdapter);
            if (match.RequiresAsyncContinuation)
            {
                var asyncCacheKey = $"{match.IntegrationTypeName}|{match.TargetTypeName}|async|{match.AsyncResultTypeName ?? "<object>"}";
                if (!asyncMethodCache.TryGetValue(asyncCacheKey, out asyncAdapter))
                {
                    asyncAdapter = EmitAsyncMethod(module, bootstrapType, integrationType, targetType, targetMethod, match, duckBindings);
                    asyncMethodCache[asyncCacheKey] = asyncAdapter;
                }
            }

            var asyncTaskResultContinuationMethod = asyncAdapter.Method is not null &&
                                                   asyncAdapter.ResultTypeReference is not null &&
                                                   IsTaskLikeReturn(targetMethod.ReturnType) &&
                                                   targetMethod.ReturnType is GenericInstanceType
                                                       ? EmitAsyncTaskResultContinuationMethod(module, bootstrapType, integrationType, targetType, targetMethod, match, asyncAdapter)
                                                       : null;
            MethodDefinition? asyncTaskResultHelperRootMethod = null;

            var endCacheKey = $"{match.IntegrationTypeName}|{match.TargetTypeName}|end|{match.ReturnTypeName}";
            if (!endMethodCache.TryGetValue(endCacheKey, out var endMethod))
            {
                endMethod = EmitEndMethod(module, bootstrapType, integrationType, targetType, targetMethod, match, duckBindings, asyncAdapter, asyncTaskResultContinuationMethod, out asyncTaskResultHelperRootMethod);
                endMethodCache[endCacheKey] = endMethod;
            }

            emittedMethods[match] = new GeneratedAdapterSet(
                module.ImportReference(integrationType),
                module.ImportReference(targetType),
                match.ReturnsValue ? module.ImportReference(targetMethod.ReturnType) : null,
                targetMethod.Parameters.Select(parameter => module.ImportReference(parameter.ParameterType)).ToList(),
                asyncAdapter.ResultTypeReference,
                beginMethod,
                endMethod,
                asyncAdapter.Method,
                asyncTaskResultContinuationMethod,
                asyncTaskResultHelperRootMethod,
                asyncAdapter.PreserveContext,
                asyncAdapter.IsAsyncCallback);
        }

        return emittedMethods;
    }

    /// <summary>
    /// Emits a begin adapter method for the supplied integration binding.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="bootstrapType">The generated bootstrap type.</param>
    /// <param name="integrationType">The tracer integration type definition.</param>
    /// <param name="targetType">The concrete matched target type definition.</param>
    /// <param name="match">The matched definition that identifies the binding.</param>
    /// <returns>The emitted adapter method.</returns>
    private static MethodDefinition EmitBeginMethod(
        ModuleDefinition module,
        TypeDefinition bootstrapType,
        TypeDefinition integrationType,
        TypeDefinition targetType,
        MethodDefinition targetMethod,
        CallTargetAotMatchedDefinition match,
        ResolvedDuckTypeBindings duckBindings)
    {
        if (match.UsesSlowBegin)
        {
            return EmitSlowBeginMethod(module, bootstrapType, integrationType, targetType, targetMethod, match, duckBindings);
        }

        var integrationMethod = integrationType.Methods.Single(candidate =>
            candidate.Name == "OnMethodBegin" &&
            candidate.Parameters.Count == match.BeginArgumentCount + 1 &&
            candidate.GenericParameters.Count == match.BeginArgumentCount + 1);
        var method = new MethodDefinition(
            $"CreateBegin_{SanitizeName(match.TargetAssemblyName)}_{SanitizeName(targetType.Name)}_{SanitizeName(match.TargetMethodName)}",
            Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
            module.ImportReference(integrationMethod.ReturnType));
        method.Parameters.Add(new ParameterDefinition("instance", Mono.Cecil.ParameterAttributes.None, module.ImportReference(targetType)));
        foreach (var parameter in targetMethod.Parameters)
        {
            method.Parameters.Add(new ParameterDefinition(parameter.Name, Mono.Cecil.ParameterAttributes.None, new ByReferenceType(module.ImportReference(parameter.ParameterType))));
        }

        bootstrapType.Methods.Add(method);

        var genericArguments = new List<TypeReference> { duckBindings.InstanceProxyTypeReference ?? module.ImportReference(targetType) };
        for (var parameterIndex = 0; parameterIndex < targetMethod.Parameters.Count; parameterIndex++)
        {
            genericArguments.Add(duckBindings.GetParameterProxyType(parameterIndex) ?? module.ImportReference(targetMethod.Parameters[parameterIndex].ParameterType));
        }

        var preserveContext = HasPreserveContextAttribute(integrationMethod);
        var closedIntegrationMethod = CreateClosedIntegrationMethodReference(module, integrationType, integrationMethod, targetType.Module, genericArguments);

        var il = method.Body.GetILProcessor();
        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        if (duckBindings.InstanceProxyTypeDefinition is not null)
        {
            EmitCreateNewProxyInstance(il, module, duckBindings.InstanceProxyTypeDefinition, module.ImportReference(targetType));
        }

        for (var index = 0; index < targetMethod.Parameters.Count; index++)
        {
            var generatedParameter = method.Parameters[index + 1];
            var importedParameterType = module.ImportReference(targetMethod.Parameters[index].ParameterType);
            il.Append(Instruction.Create(OpCodes.Ldarg, generatedParameter));
            il.Append(Instruction.Create(OpCodes.Ldobj, importedParameterType));
            if (duckBindings.GetParameterProxyTypeDefinition(index) is { } parameterProxyTypeDefinition)
            {
                EmitCreateNewProxyInstance(il, module, parameterProxyTypeDefinition, importedParameterType);
            }
        }

        il.Append(Instruction.Create(OpCodes.Call, closedIntegrationMethod));
        il.Append(Instruction.Create(OpCodes.Ret));
        return method;
    }

    /// <summary>
    /// Emits a slow-begin adapter method that receives the target arguments through an object array and unboxes or
    /// proxies them before invoking the integration callback.
    /// </summary>
    private static MethodDefinition EmitSlowBeginMethod(
        ModuleDefinition module,
        TypeDefinition bootstrapType,
        TypeDefinition integrationType,
        TypeDefinition targetType,
        MethodDefinition targetMethod,
        CallTargetAotMatchedDefinition match,
        ResolvedDuckTypeBindings duckBindings)
    {
        var integrationMethod = integrationType.Methods.Single(candidate =>
            candidate.Name == "OnMethodBegin" &&
            candidate.Parameters.Count == match.BeginArgumentCount + 1 &&
            candidate.GenericParameters.Count == match.BeginArgumentCount + 1);
        var method = new MethodDefinition(
            $"CreateBeginSlow_{SanitizeName(match.TargetAssemblyName)}_{SanitizeName(targetType.Name)}_{SanitizeName(match.TargetMethodName)}",
            Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
            module.ImportReference(integrationMethod.ReturnType));
        method.Parameters.Add(new ParameterDefinition("instance", Mono.Cecil.ParameterAttributes.None, module.ImportReference(targetType)));
        method.Parameters.Add(new ParameterDefinition("arguments", Mono.Cecil.ParameterAttributes.None, new ArrayType(module.TypeSystem.Object)));
        bootstrapType.Methods.Add(method);

        var mustLoadInstance = integrationMethod.Parameters.Count != targetMethod.Parameters.Count;
        var genericArguments = new List<TypeReference> { duckBindings.InstanceProxyTypeReference ?? module.ImportReference(targetType) };
        for (var parameterIndex = 0; parameterIndex < targetMethod.Parameters.Count; parameterIndex++)
        {
            genericArguments.Add(duckBindings.GetParameterProxyType(parameterIndex) ?? module.ImportReference(targetMethod.Parameters[parameterIndex].ParameterType));
        }

        var closedIntegrationMethod = CreateClosedIntegrationMethodReference(module, integrationType, integrationMethod, targetType.Module, genericArguments);
        var il = method.Body.GetILProcessor();
        if (mustLoadInstance)
        {
            il.Append(Instruction.Create(OpCodes.Ldarg_0));
            if (duckBindings.InstanceProxyTypeDefinition is not null)
            {
                EmitCreateNewProxyInstance(il, module, duckBindings.InstanceProxyTypeDefinition, module.ImportReference(targetType));
            }
        }

        for (var parameterIndex = 0; parameterIndex < targetMethod.Parameters.Count; parameterIndex++)
        {
            var sourceParameterType = targetMethod.Parameters[parameterIndex].ParameterType;
            if (sourceParameterType is ByReferenceType)
            {
                throw new InvalidOperationException($"The slow CallTarget NativeAOT begin path does not support by-ref arguments for '{match.TargetTypeName}.{match.TargetMethodName}'.");
            }

            var importedSourceParameterType = module.ImportReference(sourceParameterType);
            il.Append(Instruction.Create(OpCodes.Ldarg_1));
            il.Append(Instruction.Create(OpCodes.Ldc_I4, parameterIndex));
            il.Append(Instruction.Create(OpCodes.Ldelem_Ref));
            if (sourceParameterType.IsValueType)
            {
                il.Append(Instruction.Create(OpCodes.Unbox_Any, importedSourceParameterType));
            }
            else
            {
                il.Append(Instruction.Create(OpCodes.Castclass, importedSourceParameterType));
            }

            if (duckBindings.GetParameterProxyTypeDefinition(parameterIndex) is { } parameterProxyTypeDefinition)
            {
                EmitCreateNewProxyInstance(il, module, parameterProxyTypeDefinition, importedSourceParameterType);
            }
        }

        il.Append(Instruction.Create(OpCodes.Call, closedIntegrationMethod));
        il.Append(Instruction.Create(OpCodes.Ret));
        return method;
    }

    /// <summary>
    /// Emits an end adapter method for the supplied integration binding.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="bootstrapType">The generated bootstrap type.</param>
    /// <param name="integrationType">The tracer integration type definition.</param>
    /// <param name="targetType">The concrete matched target type definition.</param>
    /// <param name="match">The matched definition that identifies the binding.</param>
    /// <returns>The emitted adapter method.</returns>
    private static MethodDefinition EmitEndMethod(
        ModuleDefinition module,
        TypeDefinition bootstrapType,
        TypeDefinition integrationType,
        TypeDefinition targetType,
        MethodDefinition targetMethod,
        CallTargetAotMatchedDefinition match,
        ResolvedDuckTypeBindings duckBindings,
        GeneratedAsyncAdapter asyncAdapter,
        MethodDefinition? asyncTaskResultContinuationMethod,
        out MethodDefinition? asyncTaskResultHelperRootMethod)
    {
        asyncTaskResultHelperRootMethod = null;
        var integrationMethod = integrationType.Methods.Single(candidate =>
            candidate.Name == "OnMethodEnd" &&
            candidate.Parameters.Count == (match.ReturnsValue ? 4 : 3) &&
            candidate.GenericParameters.Count == (match.ReturnsValue ? 2 : 1));
        var emittedReturnType = match.ReturnsValue
                                    ? module.ImportReference(targetMethod.ReturnType)
                                    : module.ImportReference(integrationMethod.ReturnType);
        var method = new MethodDefinition(
            $"CreateEnd_{SanitizeName(match.TargetAssemblyName)}_{SanitizeName(targetType.Name)}_{SanitizeName(match.TargetMethodName)}",
            Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
            emittedReturnType);
        method.Parameters.Add(new ParameterDefinition("instance", Mono.Cecil.ParameterAttributes.None, module.ImportReference(targetType)));
        if (match.ReturnsValue)
        {
            method.Parameters.Add(new ParameterDefinition("returnValue", Mono.Cecil.ParameterAttributes.None, module.ImportReference(targetMethod.ReturnType)));
        }

        method.Parameters.Add(new ParameterDefinition("exception", Mono.Cecil.ParameterAttributes.None, CreateTargetSystemTypeReference(module, targetType.Module, "Exception")));
        method.Parameters.Add(new ParameterDefinition("state", Mono.Cecil.ParameterAttributes.None, module.ImportReference(integrationMethod.Parameters[match.ReturnsValue ? 3 : 2].ParameterType)));
        bootstrapType.Methods.Add(method);

        var genericArguments = new List<TypeReference> { duckBindings.InstanceProxyTypeReference ?? module.ImportReference(targetType) };
        if (match.ReturnsValue)
        {
            genericArguments.Add(duckBindings.ReturnProxyTypeReference ?? module.ImportReference(targetMethod.ReturnType));
        }

        var closedIntegrationMethod = CreateClosedIntegrationMethodReference(module, integrationType, integrationMethod, targetType.Module, genericArguments);
        GenericInstanceType? closedCallTargetReturnType = null;
        MethodReference? getReturnValueMethod = null;
        if (match.ReturnsValue)
        {
            closedCallTargetReturnType = new GenericInstanceType(module.ImportReference(integrationMethod.ReturnType.Resolve()!));
            closedCallTargetReturnType.GenericArguments.Add(module.ImportReference(targetMethod.ReturnType));
            var invokerType = ResolveType(integrationType.Module, "Datadog.Trace.ClrProfiler.CallTarget.CallTargetAotInvoker")
                              ?? throw new InvalidOperationException($"The CallTarget AOT invoker helper could not be resolved from '{integrationType.Module.FileName}'.");
            var getReturnValueInvokerMethodDefinition = invokerType.Methods.Single(candidate =>
                string.Equals(candidate.Name, duckBindings.ReturnProxyTypeReference is null ? "GetCallTargetReturnValue" : "GetDuckTypeCallTargetReturnValue", StringComparison.Ordinal) &&
                candidate.GenericParameters.Count == (duckBindings.ReturnProxyTypeReference is null ? 1 : 2) &&
                candidate.Parameters.Count == 1);
            var getReturnValueGenericArguments = duckBindings.ReturnProxyTypeReference is null
                                                    ? [module.ImportReference(targetMethod.ReturnType)]
                                                    : new[]
                                                    {
                                                        duckBindings.ReturnProxyTypeReference,
                                                        module.ImportReference(targetMethod.ReturnType),
                                                    };
            getReturnValueMethod = CreateClosedImportedMethodReference(
                getReturnValueInvokerMethodDefinition,
                module,
                getReturnValueGenericArguments);
        }

        var il = method.Body.GetILProcessor();
        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        if (duckBindings.InstanceProxyTypeDefinition is not null)
        {
            EmitCreateNewProxyInstance(il, module, duckBindings.InstanceProxyTypeDefinition, module.ImportReference(targetType));
        }

        if (match.ReturnsValue)
        {
            il.Append(Instruction.Create(OpCodes.Ldarg_1));
            if (duckBindings.ReturnProxyTypeDefinition is not null)
            {
                EmitCreateNewProxyInstance(il, module, duckBindings.ReturnProxyTypeDefinition, module.ImportReference(targetMethod.ReturnType));
            }

            il.Append(Instruction.Create(OpCodes.Ldarg_2));
            il.Append(Instruction.Create(OpCodes.Ldarg_3));
        }
        else
        {
            il.Append(Instruction.Create(OpCodes.Ldarg_1));
            il.Append(Instruction.Create(OpCodes.Ldarg_2));
        }

        il.Append(Instruction.Create(OpCodes.Call, closedIntegrationMethod));
        if (match.ReturnsValue)
        {
            il.Append(Instruction.Create(OpCodes.Call, getReturnValueMethod!));
        }

        il.Append(Instruction.Create(OpCodes.Ret));

        return method;
    }

    /// <summary>
    /// Emits an unreachable helper that directly calls the closed task-result runtime helper so NativeAOT roots the
    /// exact helper body and method-handle combination used by the generated end adapter.
    /// </summary>
    private static MethodDefinition EmitAsyncTaskResultHelperRootMethod(
        ModuleDefinition module,
        TypeDefinition bootstrapType,
        TypeDefinition targetType,
        CallTargetAotMatchedDefinition match,
        GeneratedAsyncAdapter asyncAdapter,
        GenericInstanceMethod closedInvokeEndTaskResultAndContinueMethod,
        MethodDefinition targetMethod)
    {
        var method = new MethodDefinition(
            $"RootAsyncTaskResultHelper_{SanitizeName(match.TargetAssemblyName)}_{SanitizeName(targetType.Name)}_{SanitizeName(match.TargetMethodName)}",
            Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
            module.TypeSystem.Void);
        bootstrapType.Methods.Add(method);
        method.Body.InitLocals = true;
        var il = method.Body.GetILProcessor();
        EmitDefaultValue(il, method, module.ImportReference(targetType), module);
        EmitDefaultValue(il, method, module.ImportReference(targetMethod.ReturnType), module);
        EmitDefaultValue(il, method, CreateTargetSystemTypeReference(module, targetType.Module, "Exception"), module);
        EmitDefaultValue(il, method, module.ImportReference(typeof(CallTargetState)), module);
        il.Append(asyncAdapter.PreserveContext ? Instruction.Create(OpCodes.Ldc_I4_1) : Instruction.Create(OpCodes.Ldc_I4_0));
        il.Append(asyncAdapter.IsAsyncCallback ? Instruction.Create(OpCodes.Ldc_I4_1) : Instruction.Create(OpCodes.Ldc_I4_0));
        il.Append(Instruction.Create(OpCodes.Call, closedInvokeEndTaskResultAndContinueMethod));
        il.Append(Instruction.Create(OpCodes.Pop));
        il.Append(Instruction.Create(OpCodes.Ret));
        return method;
    }

    /// <summary>
    /// Emits an async-end continuation adapter method for the supplied integration binding when the target return
    /// shape requires one and the integration defines <c>OnAsyncMethodEnd</c>.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="bootstrapType">The generated bootstrap type.</param>
    /// <param name="integrationType">The tracer integration type definition.</param>
    /// <param name="targetType">The concrete matched target type definition.</param>
    /// <param name="targetMethod">The matched target method definition.</param>
    /// <param name="match">The matched definition that identifies the binding.</param>
    /// <returns>The emitted async adapter metadata.</returns>
    private static GeneratedAsyncAdapter EmitAsyncMethod(
        ModuleDefinition module,
        TypeDefinition bootstrapType,
        TypeDefinition integrationType,
        TypeDefinition targetType,
        MethodDefinition targetMethod,
        CallTargetAotMatchedDefinition match,
        ResolvedDuckTypeBindings duckBindings)
    {
        if (!match.RequiresAsyncContinuation)
        {
            return default;
        }

        var integrationMethod = integrationType.Methods.FirstOrDefault(candidate =>
            candidate.Name == "OnAsyncMethodEnd" &&
            candidate.Parameters.Count == 4 &&
            candidate.GenericParameters.Count == (match.AsyncResultTypeName is null ? 1 : 2));
        if (integrationMethod is null)
        {
            return default;
        }

        var preserveContext = HasPreserveContextAttribute(integrationMethod);
        var asyncResultTypeReference = match.AsyncResultTypeName is null
                                           ? CreateTargetSystemTypeReference(module, targetType.Module, "Object")
                                           : GetAsyncContinuationResultTypeReference(module, targetMethod.ReturnType)
                                             ?? throw new InvalidOperationException($"The async result type for '{match.TargetTypeName}.{match.TargetMethodName}' could not be resolved from '{targetMethod.ReturnType.FullName}'.");
        var emittedReturnType = CreateClosedAsyncAdapterReturnType(module, targetType.Module, integrationMethod, asyncResultTypeReference);
        var method = new MethodDefinition(
            $"CreateAsyncEnd_{SanitizeName(match.TargetAssemblyName)}_{SanitizeName(targetType.Name)}_{SanitizeName(match.TargetMethodName)}",
            Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
            emittedReturnType);
        method.Parameters.Add(new ParameterDefinition("instance", Mono.Cecil.ParameterAttributes.None, module.ImportReference(targetType)));
        method.Parameters.Add(new ParameterDefinition("returnValue", Mono.Cecil.ParameterAttributes.None, asyncResultTypeReference));
        method.Parameters.Add(new ParameterDefinition("exception", Mono.Cecil.ParameterAttributes.None, CreateTargetSystemTypeReference(module, targetType.Module, "Exception")));
        method.Parameters.Add(new ParameterDefinition("state", Mono.Cecil.ParameterAttributes.None, CreateGeneratedAsyncStateParameterType(module, integrationMethod)));
        bootstrapType.Methods.Add(method);

        var genericArguments = new List<TypeReference> { duckBindings.InstanceProxyTypeReference ?? module.ImportReference(targetType) };
        if (integrationMethod.GenericParameters.Count == 2)
        {
            genericArguments.Add(duckBindings.AsyncResultProxyTypeReference ?? asyncResultTypeReference);
        }

        var closedIntegrationMethod = CreateClosedIntegrationMethodReference(module, integrationType, integrationMethod, targetType.Module, genericArguments);
        var il = method.Body.GetILProcessor();
        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        if (duckBindings.InstanceProxyTypeDefinition is not null)
        {
            EmitCreateNewProxyInstance(il, module, duckBindings.InstanceProxyTypeDefinition, module.ImportReference(targetType));
        }

        il.Append(Instruction.Create(OpCodes.Ldarg_1));
        if (duckBindings.AsyncResultProxyTypeDefinition is not null)
        {
            EmitCreateNewProxyInstance(il, module, duckBindings.AsyncResultProxyTypeDefinition, asyncResultTypeReference);
        }

        il.Append(Instruction.Create(OpCodes.Ldarg_2));
        EmitAsyncStateArgument(il, method.Parameters[3], integrationMethod.Parameters[3].ParameterType, module);
        il.Append(Instruction.Create(OpCodes.Call, closedIntegrationMethod));
        if (duckBindings.AsyncResultProxyTypeReference is not null)
        {
            var invokerType = ResolveType(integrationType.Module, "Datadog.Trace.ClrProfiler.CallTarget.CallTargetAotInvoker")
                              ?? throw new InvalidOperationException($"The CallTarget AOT invoker helper could not be resolved from '{integrationType.Module.FileName}'.");
            var unwrapMethodDefinition = invokerType.Methods.Single(candidate =>
                string.Equals(candidate.Name, IsTaskLikeReturn(integrationMethod.ReturnType) ? "UnwrapTaskReturnValue" : "UnwrapReturnValue", StringComparison.Ordinal) &&
                candidate.GenericParameters.Count == 2);
            var unwrapMethod = CreateClosedImportedMethodReference(
                unwrapMethodDefinition,
                module,
                [duckBindings.AsyncResultProxyTypeReference, asyncResultTypeReference]);
            if (IsTaskLikeReturn(integrationMethod.ReturnType))
            {
                il.Append(preserveContext ? Instruction.Create(OpCodes.Ldc_I4_1) : Instruction.Create(OpCodes.Ldc_I4_0));
            }

            il.Append(Instruction.Create(OpCodes.Call, unwrapMethod));
        }

        il.Append(Instruction.Create(OpCodes.Ret));

        return new GeneratedAsyncAdapter(
            method,
            match.AsyncResultTypeName is null ? null : asyncResultTypeReference,
            preserveContext,
            IsTaskLikeReturn(integrationMethod.ReturnType));
    }

    /// <summary>
    /// Emits a typed task-return continuation wrapper that delegates to the generated async callback through the
    /// runtime helper without constructing any runtime generic continuation types in the NativeAOT app.
    /// </summary>
    private static MethodDefinition EmitAsyncTaskResultContinuationMethod(
        ModuleDefinition module,
        TypeDefinition bootstrapType,
        TypeDefinition integrationType,
        TypeDefinition targetType,
        MethodDefinition targetMethod,
        CallTargetAotMatchedDefinition match,
        GeneratedAsyncAdapter asyncAdapter)
    {
        if (asyncAdapter.Method is null || asyncAdapter.ResultTypeReference is null)
        {
            throw new InvalidOperationException($"The typed task-return continuation for '{match.TargetTypeName}.{match.TargetMethodName}' requires a generated async callback.");
        }

        var method = new MethodDefinition(
            $"ContinueAsyncEnd_{SanitizeName(match.TargetAssemblyName)}_{SanitizeName(targetType.Name)}_{SanitizeName(match.TargetMethodName)}",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
            module.ImportReference(targetMethod.ReturnType));
        method.Parameters.Add(new ParameterDefinition("instance", Mono.Cecil.ParameterAttributes.None, module.ImportReference(targetType)));
        method.Parameters.Add(new ParameterDefinition("returnValue", Mono.Cecil.ParameterAttributes.None, module.ImportReference(targetMethod.ReturnType)));
        method.Parameters.Add(new ParameterDefinition("exception", Mono.Cecil.ParameterAttributes.None, CreateTargetSystemTypeReference(module, targetType.Module, "Exception")));
        method.Parameters.Add(new ParameterDefinition("state", Mono.Cecil.ParameterAttributes.None, module.ImportReference(typeof(CallTargetState))));
        bootstrapType.Methods.Add(method);

        var invokerType = ResolveType(integrationType.Module, "Datadog.Trace.ClrProfiler.CallTarget.CallTargetAotInvoker")
                          ?? throw new InvalidOperationException($"The CallTarget AOT invoker helper could not be resolved from '{integrationType.Module.FileName}'.");
        var continuationHelperMethodName = IsValueTaskResult(targetMethod.ReturnType)
                                               ? "ContinueValueTaskResultFromMethodHandle"
                                               : "ContinueTaskResultFromMethodHandle";
        var continueTaskResultMethodDefinition = invokerType.Methods.Single(candidate =>
            string.Equals(candidate.Name, continuationHelperMethodName, StringComparison.Ordinal) &&
            candidate.GenericParameters.Count == 3 &&
            candidate.Parameters.Count == 7);
        var closedContinueTaskResultMethod = CreateClosedImportedMethodReference(
            continueTaskResultMethodDefinition,
            module,
            [
                module.ImportReference(integrationType),
                module.ImportReference(targetType),
                module.ImportReference(asyncAdapter.ResultTypeReference),
            ]);

        var il = method.Body.GetILProcessor();
        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        il.Append(Instruction.Create(OpCodes.Ldarg_1));
        il.Append(Instruction.Create(OpCodes.Ldarg_2));
        il.Append(Instruction.Create(OpCodes.Ldarg_3));
        il.Append(Instruction.Create(OpCodes.Ldtoken, asyncAdapter.Method));
        il.Append(asyncAdapter.PreserveContext ? Instruction.Create(OpCodes.Ldc_I4_1) : Instruction.Create(OpCodes.Ldc_I4_0));
        il.Append(asyncAdapter.IsAsyncCallback ? Instruction.Create(OpCodes.Ldc_I4_1) : Instruction.Create(OpCodes.Ldc_I4_0));
        il.Append(Instruction.Create(OpCodes.Call, closedContinueTaskResultMethod));
        il.Append(Instruction.Create(OpCodes.Ret));
        return method;
    }

    /// <summary>
    /// Extracts the typed async continuation result type from a Task{TResult} or ValueTask{TResult} return type.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="targetReturnType">The matched target method return type.</param>
    /// <returns>The imported continuation result type or <see langword="null"/> when the target return is not generic.</returns>
    private static TypeReference? GetAsyncContinuationResultTypeReference(ModuleDefinition module, TypeReference targetReturnType)
    {
        return targetReturnType is GenericInstanceType genericInstanceType && genericInstanceType.GenericArguments.Count == 1
                   ? module.ImportReference(genericInstanceType.GenericArguments[0])
                   : null;
    }

    /// <summary>
    /// Resolves the async adapter return type for the current exact-shape async AOT support.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="targetModule">The target module whose runtime identity should be matched for system types.</param>
    /// <param name="integrationMethod">The resolved <c>OnAsyncMethodEnd</c> integration method.</param>
    /// <param name="asyncResultTypeReference">The resolved async continuation result type.</param>
    /// <returns>The generated adapter return type.</returns>
    private static TypeReference CreateClosedAsyncAdapterReturnType(
        ModuleDefinition module,
        ModuleDefinition targetModule,
        MethodDefinition integrationMethod,
        TypeReference asyncResultTypeReference)
    {
        if (integrationMethod.ReturnType is GenericParameter genericParameter &&
            genericParameter.Type == GenericParameterType.Method &&
            genericParameter.Position == 1)
        {
            return asyncResultTypeReference;
        }

        if (integrationMethod.ReturnType is GenericInstanceType genericInstanceType &&
            string.Equals(genericInstanceType.ElementType.FullName, "System.Threading.Tasks.Task`1", StringComparison.Ordinal) &&
            genericInstanceType.GenericArguments.Count == 1 &&
            genericInstanceType.GenericArguments[0] is GenericParameter returnGenericParameter &&
            returnGenericParameter.Type == GenericParameterType.Method &&
            returnGenericParameter.Position == 1)
        {
            var closedTaskType = new GenericInstanceType(CreateTargetRuntimeTypeReference(module, targetModule, "System.Threading.Tasks", "Task`1"));
            closedTaskType.GenericArguments.Add(asyncResultTypeReference);
            return closedTaskType;
        }

        return CreateIntegrationParameterType(module, targetModule, integrationMethod.ReturnType, []);
    }

    /// <summary>
    /// Creates the generated async adapter state parameter type so the emitted adapter always matches the continuation
    /// delegate shape even when the integration accepts the state by value.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="integrationMethod">The integration async method definition.</param>
    /// <returns>The generated async adapter state parameter type.</returns>
    private static TypeReference CreateGeneratedAsyncStateParameterType(ModuleDefinition module, MethodDefinition integrationMethod)
    {
        return integrationMethod.Parameters[3].ParameterType is ByReferenceType byReferenceType
                   ? module.ImportReference(integrationMethod.Parameters[3].ParameterType)
                   : new ByReferenceType(module.ImportReference(integrationMethod.Parameters[3].ParameterType));
    }

    /// <summary>
    /// Emits the correct state argument load for async adapters, dereferencing the by-ref generated parameter when the
    /// integration expects the state by value.
    /// </summary>
    /// <param name="il">The IL processor for the generated method.</param>
    /// <param name="stateParameter">The generated adapter state parameter.</param>
    /// <param name="integrationStateParameterType">The integration state parameter type.</param>
    /// <param name="module">The generated module.</param>
    private static void EmitAsyncStateArgument(ILProcessor il, ParameterDefinition stateParameter, TypeReference integrationStateParameterType, ModuleDefinition module)
    {
        il.Append(Instruction.Create(OpCodes.Ldarg, stateParameter));
        if (integrationStateParameterType is ByReferenceType)
        {
            return;
        }

        il.Append(Instruction.Create(OpCodes.Ldobj, module.ImportReference(integrationStateParameterType)));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the integration method is annotated with <c>PreserveContextAttribute</c>.
    /// </summary>
    /// <param name="integrationMethod">The integration method to inspect.</param>
    /// <returns><see langword="true"/> when the attribute is present; otherwise <see langword="false"/>.</returns>
    private static bool HasPreserveContextAttribute(MethodDefinition integrationMethod)
    {
        return integrationMethod.CustomAttributes.Any(attribute => string.Equals(attribute.AttributeType.FullName, "Datadog.Trace.ClrProfiler.CallTarget.PreserveContextAttribute", StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the supplied return type is <see cref="System.Threading.Tasks.Task"/> or <see cref="System.Threading.Tasks.Task{TResult}"/>.
    /// </summary>
    /// <param name="returnType">The return type to inspect.</param>
    /// <returns><see langword="true"/> when the return type is task-based; otherwise <see langword="false"/>.</returns>
    private static bool IsTaskLikeReturn(TypeReference returnType)
    {
        return string.Equals(returnType.FullName, "System.Threading.Tasks.Task", StringComparison.Ordinal) ||
               (returnType is GenericInstanceType genericInstanceType &&
                string.Equals(genericInstanceType.ElementType.FullName, "System.Threading.Tasks.Task`1", StringComparison.Ordinal));
    }

    /// <summary>
    /// Creates a closed generic integration method reference using explicit parameter imports so the generated
    /// adapter does not inherit invalid core-library metadata from the source method import.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="integrationType">The tracer integration type definition.</param>
    /// <param name="integrationMethod">The generic integration method definition.</param>
    /// <param name="targetModule">The target module whose runtime identity should be matched for system types.</param>
    /// <param name="genericArguments">The concrete generic arguments used to close the integration method.</param>
    /// <returns>The closed generic method reference used by the generated adapter call site.</returns>
    private static GenericInstanceMethod CreateClosedIntegrationMethodReference(
        ModuleDefinition module,
        TypeDefinition integrationType,
        MethodDefinition integrationMethod,
        ModuleDefinition targetModule,
        IReadOnlyList<TypeReference> genericArguments)
    {
        var importedDeclaringType = module.ImportReference(integrationType);
        var importedMethod = new MethodReference(integrationMethod.Name, module.TypeSystem.Void, importedDeclaringType)
        {
            HasThis = integrationMethod.HasThis,
            ExplicitThis = integrationMethod.ExplicitThis,
            CallingConvention = integrationMethod.CallingConvention,
        };

        var methodGenericParameters = new List<GenericParameter>(integrationMethod.GenericParameters.Count);
        foreach (var sourceGenericParameter in integrationMethod.GenericParameters)
        {
            var importedGenericParameter = new GenericParameter(sourceGenericParameter.Name, importedMethod);
            importedMethod.GenericParameters.Add(importedGenericParameter);
            methodGenericParameters.Add(importedGenericParameter);
        }

        importedMethod.ReturnType = CreateIntegrationParameterType(module, targetModule, integrationMethod.ReturnType, methodGenericParameters);

        for (var index = 0; index < integrationMethod.Parameters.Count; index++)
        {
            var sourceParameter = integrationMethod.Parameters[index];
            var importedParameterType = CreateIntegrationParameterType(module, targetModule, sourceParameter.ParameterType, methodGenericParameters);
            importedMethod.Parameters.Add(new ParameterDefinition(sourceParameter.Name, sourceParameter.Attributes, importedParameterType));
        }

        var closedMethod = new GenericInstanceMethod(importedMethod);
        foreach (var genericArgument in genericArguments)
        {
            closedMethod.GenericArguments.Add(module.ImportReference(genericArgument));
        }

        return closedMethod;
    }

    /// <summary>
    /// Imports a generic helper method definition from another assembly and closes it with the supplied concrete
    /// generic arguments while preserving the target application's runtime identities for system types.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="declaringType">The declaring type that owns the helper method.</param>
    /// <param name="methodDefinition">The generic helper method definition to import.</param>
    /// <param name="targetModule">The target application module whose runtime identity should be matched.</param>
    /// <param name="genericArguments">The concrete generic arguments used to close the helper method.</param>
    /// <returns>The closed imported helper method reference.</returns>
    private static GenericInstanceMethod CreateClosedImportedMethodReference(
        MethodDefinition methodDefinition,
        ModuleDefinition module,
        IReadOnlyList<TypeReference> genericArguments)
    {
        var importedDeclaringType = module.ImportReference(methodDefinition.DeclaringType);
        var importedMethod = new MethodReference(methodDefinition.Name, module.TypeSystem.Void, importedDeclaringType)
        {
            HasThis = methodDefinition.HasThis,
            ExplicitThis = methodDefinition.ExplicitThis,
            CallingConvention = methodDefinition.CallingConvention,
        };

        var methodGenericParameters = new List<GenericParameter>(methodDefinition.GenericParameters.Count);
        foreach (var sourceGenericParameter in methodDefinition.GenericParameters)
        {
            var importedGenericParameter = new GenericParameter(sourceGenericParameter.Name, importedMethod);
            importedMethod.GenericParameters.Add(importedGenericParameter);
            methodGenericParameters.Add(importedGenericParameter);
        }

        importedMethod.ReturnType = CreateImportedMethodParameterType(module, methodDefinition.ReturnType, methodGenericParameters);
        foreach (var sourceParameter in methodDefinition.Parameters)
        {
            importedMethod.Parameters.Add(new ParameterDefinition(
                sourceParameter.Name,
                sourceParameter.Attributes,
                CreateImportedMethodParameterType(module, sourceParameter.ParameterType, methodGenericParameters)));
        }

        var closedMethod = new GenericInstanceMethod(importedMethod);
        foreach (var genericArgument in genericArguments)
        {
            closedMethod.GenericArguments.Add(module.ImportReference(genericArgument));
        }

        return closedMethod;
    }

    /// <summary>
    /// Imports a helper-method parameter or return type while preserving the original defining assembly scope for
    /// framework types so the generated helper call matches the method that already exists in Datadog.Trace.
    /// </summary>
    private static TypeReference CreateImportedMethodParameterType(
        ModuleDefinition module,
        TypeReference parameterType,
        IReadOnlyList<GenericParameter> methodGenericParameters)
    {
        if (parameterType is GenericParameter genericParameter &&
            genericParameter.Type == GenericParameterType.Method)
        {
            return methodGenericParameters[genericParameter.Position];
        }

        if (parameterType is ByReferenceType byReferenceType)
        {
            return new ByReferenceType(CreateImportedMethodParameterType(module, byReferenceType.ElementType, methodGenericParameters));
        }

        if (parameterType is GenericInstanceType genericInstanceType)
        {
            var importedElementType = CreateImportedMethodParameterType(module, genericInstanceType.ElementType, methodGenericParameters);
            var importedGenericInstanceType = new GenericInstanceType(importedElementType);
            foreach (var genericArgument in genericInstanceType.GenericArguments)
            {
                importedGenericInstanceType.GenericArguments.Add(CreateImportedMethodParameterType(module, genericArgument, methodGenericParameters));
            }

            return importedGenericInstanceType;
        }

        if (parameterType.Scope is AssemblyNameReference assemblyReference)
        {
            return new TypeReference(
                parameterType.Namespace,
                parameterType.Name,
                module,
                EnsureAssemblyReference(module, assemblyReference),
                parameterType.IsValueType);
        }

        return module.ImportReference(parameterType);
    }

    /// <summary>
    /// Closes the generic <c>CallTargetReturn&lt;TReturn&gt;</c> method return type for a concrete target return type.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="integrationMethod">The integration method that declares the generic return wrapper.</param>
    /// <param name="targetReturnType">The concrete target return type.</param>
    /// <returns>The closed return wrapper type used by the generated end adapter.</returns>
    private static TypeReference CreateClosedValueReturnType(ModuleDefinition module, MethodDefinition integrationMethod, TypeReference targetReturnType)
    {
        if (integrationMethod.ReturnType is not GenericInstanceType genericReturnType)
        {
            throw new InvalidOperationException($"The integration end method '{integrationMethod.FullName}' does not declare a generic CallTarget return type.");
        }

        var closedReturnType = new GenericInstanceType(module.ImportReference(genericReturnType.ElementType));
        closedReturnType.GenericArguments.Add(module.ImportReference(targetReturnType));
        return closedReturnType;
    }

    /// <summary>
    /// Imports an integration method parameter type while preserving method generic parameters and ensuring
    /// system exception references are scoped to the target application's System.Runtime identity.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="targetModule">The target application module.</param>
    /// <param name="parameterType">The source integration parameter type.</param>
    /// <param name="methodGenericParameters">The imported generic parameters owned by the generated method reference.</param>
    /// <returns>The imported parameter type for the generated adapter call site.</returns>
    private static TypeReference CreateIntegrationParameterType(
        ModuleDefinition module,
        ModuleDefinition targetModule,
        TypeReference parameterType,
        IReadOnlyList<GenericParameter> methodGenericParameters)
    {
        if (parameterType is GenericParameter genericParameter &&
            genericParameter.Type == GenericParameterType.Method)
        {
            return methodGenericParameters[genericParameter.Position];
        }

        if (string.Equals(parameterType.Namespace, "System", StringComparison.Ordinal) &&
            string.Equals(parameterType.Name, "Exception", StringComparison.Ordinal))
        {
            return CreateTargetSystemTypeReference(module, targetModule, "Exception");
        }

        if (parameterType is ByReferenceType byReferenceType)
        {
            return new ByReferenceType(CreateIntegrationParameterType(module, targetModule, byReferenceType.ElementType, methodGenericParameters));
        }

        if (parameterType is GenericInstanceType genericInstanceType)
        {
            var importedGenericInstanceType = new GenericInstanceType(CreateIntegrationParameterType(module, targetModule, genericInstanceType.ElementType, methodGenericParameters));
            foreach (var genericArgument in genericInstanceType.GenericArguments)
            {
                importedGenericInstanceType.GenericArguments.Add(CreateIntegrationParameterType(module, targetModule, genericArgument, methodGenericParameters));
            }

            return importedGenericInstanceType;
        }

        if (string.Equals(parameterType.Namespace, "System", StringComparison.Ordinal) &&
            (string.Equals(parameterType.Name, "RuntimeMethodHandle", StringComparison.Ordinal) ||
             string.Equals(parameterType.Name, "RuntimeTypeHandle", StringComparison.Ordinal) ||
             string.Equals(parameterType.Name, "Type", StringComparison.Ordinal)))
        {
            return CreateTargetSystemTypeReference(module, targetModule, parameterType.Name);
        }

        if (IsTargetRuntimeTaskType(parameterType))
        {
            return CreateTargetRuntimeTypeReference(module, targetModule, parameterType.Namespace, parameterType.Name);
        }

        return module.ImportReference(parameterType);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the supplied type should be anchored to the target app's
    /// <c>System.Runtime</c> reference instead of the generated registry assembly.
    /// </summary>
    /// <param name="typeReference">The type to inspect.</param>
    /// <returns><see langword="true"/> when the type is a task runtime type; otherwise <see langword="false"/>.</returns>
    private static bool IsTargetRuntimeTaskType(TypeReference typeReference)
    {
        return string.Equals(typeReference.Namespace, "System.Threading.Tasks", StringComparison.Ordinal) &&
               (string.Equals(typeReference.Name, "Task", StringComparison.Ordinal) ||
                string.Equals(typeReference.Name, "Task`1", StringComparison.Ordinal) ||
                string.Equals(typeReference.Name, "ValueTask", StringComparison.Ordinal) ||
                string.Equals(typeReference.Name, "ValueTask`1", StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the supplied return type is a typed <c>ValueTask{T}</c>.
    /// </summary>
    /// <param name="typeReference">The return type to inspect.</param>
    /// <returns><see langword="true"/> when the type is <c>System.Threading.Tasks.ValueTask{T}</c>; otherwise <see langword="false"/>.</returns>
    private static bool IsValueTaskResult(TypeReference typeReference)
    {
        return typeReference is GenericInstanceType genericInstanceType &&
               string.Equals(genericInstanceType.ElementType.Namespace, "System.Threading.Tasks", StringComparison.Ordinal) &&
               string.Equals(genericInstanceType.ElementType.Name, "ValueTask`1", StringComparison.Ordinal);
    }

    /// <summary>
    /// Emits the idempotent bootstrap method that enables AOT mode and registers every generated adapter.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="bootstrapType">The generated bootstrap type.</param>
    /// <param name="initializedField">The field used to guarantee one-time initialization.</param>
    /// <param name="matchedDefinitions">The matched definitions being registered.</param>
    /// <param name="generatedAdapterMethods">The emitted adapter methods keyed by match.</param>
    private static void EmitInitializeMethod(
        ModuleDefinition module,
        TypeDefinition bootstrapType,
        ModuleDefinition tracerModule,
        ModuleDefinition targetModule,
        IReadOnlyList<CallTargetAotMatchedDefinition> matchedDefinitions,
        IReadOnlyDictionary<CallTargetAotMatchedDefinition, GeneratedAdapterSet> generatedAdapterMethods,
        CallTargetAotDuckTypeEmitterContext? duckTypeContext)
    {
        var initializeMethod = new MethodDefinition(
            BootstrapMethodName,
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
            module.TypeSystem.Boolean);
        bootstrapType.Methods.Add(initializeMethod);
        AddTypeDynamicDependency(module, initializeMethod, bootstrapType, targetModule);
        var callTargetReturnType = ResolveType(tracerModule, "Datadog.Trace.ClrProfiler.CallTarget.CallTargetReturn`1");
        foreach (var adapterSet in generatedAdapterMethods.Values.Where(static set => set.ReturnTypeReference is not null))
        {
            var closedCallTargetReturnType = new GenericInstanceType(module.ImportReference(callTargetReturnType!));
            closedCallTargetReturnType.GenericArguments.Add(module.ImportReference(adapterSet.ReturnTypeReference!));
            AddTypeDynamicDependency(module, initializeMethod, closedCallTargetReturnType, targetModule);
        }

        var callTargetAotType = ResolveType(tracerModule, "Datadog.Trace.ClrProfiler.CallTarget.CallTargetAot")
                                ?? throw new InvalidOperationException($"The CallTarget AOT runtime type could not be resolved from '{tracerModule.FileName}'.");
        var importedCallTargetAotType = module.ImportReference(callTargetAotType);
        var systemTypeReference = CreateTargetSystemTypeReference(module, targetModule, "Type");
        var runtimeTypeHandleReference = CreateTargetSystemTypeReference(module, targetModule, "RuntimeTypeHandle");
        var getTypeFromHandleMethod = new MethodReference("GetTypeFromHandle", systemTypeReference, systemTypeReference)
        {
            HasThis = false,
        };
        getTypeFromHandleMethod.Parameters.Add(new ParameterDefinition(runtimeTypeHandleReference));

        var tryInitializeGeneratedRegistryMethod = new MethodReference("TryInitializeGeneratedRegistry", module.TypeSystem.Boolean, importedCallTargetAotType)
        {
            HasThis = false,
        };
        tryInitializeGeneratedRegistryMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        var validateAotRegistryContractMethod = new MethodReference("ValidateAotRegistryContract", module.TypeSystem.Void, importedCallTargetAotType)
        {
            HasThis = false,
        };
        validateAotRegistryContractMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        validateAotRegistryContractMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        validateAotRegistryContractMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        validateAotRegistryContractMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        validateAotRegistryContractMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));

        var tracerAssemblyVersion = tracerModule.Assembly.Name.Version?.ToString() ?? "0.0.0.0";
        var tracerAssemblyMvid = tracerModule.Mvid.ToString("D");
        var registryAssemblyFullName = module.Assembly.FullName;
        var registryAssemblyMvid = module.Mvid.ToString("D");

        MethodReference? initializeDuckTypeRegistryMethod = null;
        if (duckTypeContext is not null)
        {
            initializeDuckTypeRegistryMethod = module.ImportReference(duckTypeContext.Value.BootstrapInitializeMethod);
        }

        var registerHandlerPairMethod = new MethodReference("RegisterAotHandlerPair", module.TypeSystem.Void, importedCallTargetAotType)
        {
            HasThis = false,
        };
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));

        var registerSlowHandlerPairMethod = new MethodReference("RegisterAotSlowHandlerPair", module.TypeSystem.Void, importedCallTargetAotType)
        {
            HasThis = false,
        };
        registerSlowHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerSlowHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerSlowHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerSlowHandlerPairMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerSlowHandlerPairMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        registerSlowHandlerPairMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));

        var registerAsyncHandlerMethod = new MethodReference("RegisterAotAsyncHandler", module.TypeSystem.Void, importedCallTargetAotType)
        {
            HasThis = false,
        };
        registerAsyncHandlerMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerAsyncHandlerMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerAsyncHandlerMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerAsyncHandlerMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerAsyncHandlerMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        registerAsyncHandlerMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.Boolean));
        registerAsyncHandlerMethod.Parameters.Add(new ParameterDefinition(module.TypeSystem.Boolean));

        var registerAsyncTaskResultContinuationMethod = new MethodReference("RegisterAotAsyncTaskResultContinuation", module.TypeSystem.Void, importedCallTargetAotType)
        {
            HasThis = false,
        };
        registerAsyncTaskResultContinuationMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerAsyncTaskResultContinuationMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerAsyncTaskResultContinuationMethod.Parameters.Add(new ParameterDefinition(systemTypeReference));
        registerAsyncTaskResultContinuationMethod.Parameters.Add(new ParameterDefinition(CreateTargetSystemTypeReference(module, targetModule, "RuntimeMethodHandle")));

        var il = initializeMethod.Body.GetILProcessor();
        if (matchedDefinitions.Count == 0)
        {
            if (initializeDuckTypeRegistryMethod is not null)
            {
                il.Append(Instruction.Create(OpCodes.Call, initializeDuckTypeRegistryMethod));
            }

            il.Append(Instruction.Create(OpCodes.Ldstr, CallTargetAotContract.CurrentSchemaVersion));
            il.Append(Instruction.Create(OpCodes.Ldstr, tracerAssemblyVersion));
            il.Append(Instruction.Create(OpCodes.Ldstr, tracerAssemblyMvid));
            il.Append(Instruction.Create(OpCodes.Ldstr, registryAssemblyFullName));
            il.Append(Instruction.Create(OpCodes.Ldstr, registryAssemblyMvid));
            il.Append(Instruction.Create(OpCodes.Call, validateAotRegistryContractMethod));
            il.Append(Instruction.Create(OpCodes.Ldtoken, bootstrapType));
            il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
            il.Append(Instruction.Create(OpCodes.Call, tryInitializeGeneratedRegistryMethod));
            il.Append(Instruction.Create(OpCodes.Ret));
            return;
        }

        var firstMatch = matchedDefinitions[0];
        var firstAdapterSet = generatedAdapterMethods[firstMatch];
        var registerHandlersInstruction = Instruction.Create(OpCodes.Ldtoken, firstAdapterSet.IntegrationTypeReference);
        if (initializeDuckTypeRegistryMethod is not null)
        {
            il.Append(Instruction.Create(OpCodes.Call, initializeDuckTypeRegistryMethod));
        }

        il.Append(Instruction.Create(OpCodes.Ldstr, CallTargetAotContract.CurrentSchemaVersion));
        il.Append(Instruction.Create(OpCodes.Ldstr, tracerAssemblyVersion));
        il.Append(Instruction.Create(OpCodes.Ldstr, tracerAssemblyMvid));
        il.Append(Instruction.Create(OpCodes.Ldstr, registryAssemblyFullName));
        il.Append(Instruction.Create(OpCodes.Ldstr, registryAssemblyMvid));
        il.Append(Instruction.Create(OpCodes.Call, validateAotRegistryContractMethod));
        il.Append(Instruction.Create(OpCodes.Ldtoken, bootstrapType));
        il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
        il.Append(Instruction.Create(OpCodes.Call, tryInitializeGeneratedRegistryMethod));
        il.Append(Instruction.Create(OpCodes.Brtrue_S, registerHandlersInstruction));
        il.Append(Instruction.Create(OpCodes.Ldc_I4_0));
        il.Append(Instruction.Create(OpCodes.Ret));

        for (var i = 0; i < matchedDefinitions.Count; i++)
        {
            var match = matchedDefinitions[i];
            var adapterSet = generatedAdapterMethods[match];
            if (i == 0)
            {
                il.Append(registerHandlersInstruction);
            }
            else
            {
                il.Append(Instruction.Create(OpCodes.Ldtoken, adapterSet.IntegrationTypeReference));
            }

            il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
            il.Append(Instruction.Create(OpCodes.Ldtoken, adapterSet.TargetTypeReference));
            il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
            if (adapterSet.ReturnTypeReference is not null)
            {
                il.Append(Instruction.Create(OpCodes.Ldtoken, adapterSet.ReturnTypeReference));
                il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
            }
            else
            {
                il.Append(Instruction.Create(OpCodes.Ldnull));
            }

            il.Append(Instruction.Create(OpCodes.Ldtoken, bootstrapType));
            il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
            il.Append(Instruction.Create(OpCodes.Ldstr, adapterSet.BeginMethod.Name));
            il.Append(Instruction.Create(OpCodes.Ldstr, adapterSet.EndMethod.Name));
            if (match.UsesSlowBegin)
            {
                il.Append(Instruction.Create(OpCodes.Call, registerSlowHandlerPairMethod));
            }
            else
            {
                for (var argumentIndex = 0; argumentIndex < 8; argumentIndex++)
                {
                    if (adapterSet.ArgumentTypeReferences.Count > argumentIndex)
                    {
                        il.Append(Instruction.Create(OpCodes.Ldtoken, adapterSet.ArgumentTypeReferences[argumentIndex]));
                        il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
                    }
                    else
                    {
                        il.Append(Instruction.Create(OpCodes.Ldnull));
                    }
                }

                il.Append(Instruction.Create(OpCodes.Call, registerHandlerPairMethod));
            }

            if (match.RequiresAsyncContinuation)
            {
                il.Append(Instruction.Create(OpCodes.Ldtoken, adapterSet.IntegrationTypeReference));
                il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
                il.Append(Instruction.Create(OpCodes.Ldtoken, adapterSet.TargetTypeReference));
                il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
                if (adapterSet.AsyncResultTypeReference is not null)
                {
                    il.Append(Instruction.Create(OpCodes.Ldtoken, adapterSet.AsyncResultTypeReference));
                    il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
                }
                else
                {
                    il.Append(Instruction.Create(OpCodes.Ldnull));
                }

                il.Append(Instruction.Create(OpCodes.Ldtoken, bootstrapType));
                il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
                if (adapterSet.AsyncMethod is not null)
                {
                    il.Append(Instruction.Create(OpCodes.Ldstr, adapterSet.AsyncMethod.Name));
                }
                else
                {
                    il.Append(Instruction.Create(OpCodes.Ldnull));
                }

                il.Append(adapterSet.AsyncPreserveContext ? Instruction.Create(OpCodes.Ldc_I4_1) : Instruction.Create(OpCodes.Ldc_I4_0));
                il.Append(adapterSet.AsyncIsAsyncCallback ? Instruction.Create(OpCodes.Ldc_I4_1) : Instruction.Create(OpCodes.Ldc_I4_0));
                il.Append(Instruction.Create(OpCodes.Call, registerAsyncHandlerMethod));

                if (adapterSet.AsyncTaskResultContinuationMethod is not null && adapterSet.ReturnTypeReference is not null)
                {
                    il.Append(Instruction.Create(OpCodes.Ldtoken, adapterSet.IntegrationTypeReference));
                    il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
                    il.Append(Instruction.Create(OpCodes.Ldtoken, adapterSet.TargetTypeReference));
                    il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
                    il.Append(Instruction.Create(OpCodes.Ldtoken, adapterSet.ReturnTypeReference));
                    il.Append(Instruction.Create(OpCodes.Call, getTypeFromHandleMethod));
                    il.Append(Instruction.Create(OpCodes.Ldtoken, adapterSet.AsyncTaskResultContinuationMethod));
                    il.Append(Instruction.Create(OpCodes.Call, registerAsyncTaskResultContinuationMethod));
                }
            }
        }

        il.Append(Instruction.Create(OpCodes.Ldc_I4_1));
        il.Append(Instruction.Create(OpCodes.Ret));
    }

    /// <summary>
    /// Adds a DynamicDependency attribute that keeps the generated bootstrap members available for reflection-driven
    /// AOT dispatch during NativeAOT publish.
    /// </summary>
    private static void AddTypeDynamicDependency(ModuleDefinition module, MethodDefinition initializeMethod, TypeReference dependencyType, ModuleDefinition targetModule)
    {
        var dynamicDependencyAttributeType = Type.GetType("System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute, System.Private.CoreLib", throwOnError: false)
                                           ?? Type.GetType("System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute, System.Runtime", throwOnError: false)
                                           ?? throw new InvalidOperationException("DynamicDependencyAttribute could not be resolved.");
        var dynamicallyAccessedMemberTypesType = Type.GetType("System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes, System.Private.CoreLib", throwOnError: false)
                                                ?? Type.GetType("System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes, System.Runtime", throwOnError: false)
                                                ?? throw new InvalidOperationException("DynamicallyAccessedMemberTypes could not be resolved.");
        var attributeConstructor = dynamicDependencyAttributeType.GetConstructor([dynamicallyAccessedMemberTypesType, typeof(Type)])
                                  ?? throw new InvalidOperationException("DynamicDependencyAttribute(DynamicallyAccessedMemberTypes, Type) could not be resolved.");
        var importedConstructor = module.ImportReference(attributeConstructor);
        var enumType = module.ImportReference(dynamicallyAccessedMemberTypesType);
        var systemTypeReference = CreateTargetSystemTypeReference(module, targetModule, "Type");
        var attribute = new CustomAttribute(importedConstructor);
        var allMembersValue = Enum.Parse(dynamicallyAccessedMemberTypesType, "All");
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(enumType, allMembersValue));
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(systemTypeReference, module.ImportReference(dependencyType)));
        initializeMethod.CustomAttributes.Add(attribute);
    }

    /// <summary>
    /// Emits an unreachable helper that directly references every generated typed task-result continuation wrapper so
    /// NativeAOT roots their bodies and closed generic helper instantiations.
    /// </summary>
    private static MethodDefinition? EmitTypedAsyncRootMethod(
        ModuleDefinition module,
        TypeDefinition bootstrapType,
        IEnumerable<GeneratedAdapterSet> generatedAdapterSets)
    {
        var methodsToRoot = generatedAdapterSets
                           .SelectMany(static set => new MethodDefinition?[]
                            {
                                set.BeginMethod,
                                set.EndMethod,
                                set.AsyncMethod,
                                set.AsyncTaskResultContinuationMethod,
                                set.AsyncTaskResultHelperRootMethod,
                            })
                           .Where(static method => method is not null)
                           .Cast<MethodDefinition>()
                           .Distinct()
                           .ToList();
        if (methodsToRoot.Count == 0)
        {
            return null;
        }

        var rootMethod = new MethodDefinition(
            "RootTypedAsyncTaskResultContinuations",
            Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig,
            module.TypeSystem.Void);
        bootstrapType.Methods.Add(rootMethod);

        var il = rootMethod.Body.GetILProcessor();
        foreach (var method in methodsToRoot)
        {
            foreach (var parameter in method.Parameters)
            {
                EmitDefaultValue(il, rootMethod, parameter.ParameterType, module);
            }

            il.Append(Instruction.Create(OpCodes.Call, method));
            if (method.ReturnType.MetadataType != MetadataType.Void)
            {
                il.Append(Instruction.Create(OpCodes.Pop));
            }
        }

        il.Append(Instruction.Create(OpCodes.Ret));
        return rootMethod;
    }

    /// <summary>
    /// Appends an unreachable branch from the bootstrap initializer to the typed async root method so the generated
    /// wrappers stay unused at runtime while still being compiled by NativeAOT.
    /// </summary>
    private static void AppendTypedAsyncRootBranch(ModuleDefinition module, TypeDefinition bootstrapType, MethodDefinition? typedAsyncRootMethod)
    {
        if (typedAsyncRootMethod is null)
        {
            return;
        }

        var initializeMethod = bootstrapType.Methods.Single(method => string.Equals(method.Name, BootstrapMethodName, StringComparison.Ordinal));
        var il = initializeMethod.Body.GetILProcessor();
        var firstInstruction = initializeMethod.Body.Instructions[0];
        var skipInstruction = Instruction.Create(OpCodes.Nop);
        var getEnvironmentVariableMethod = module.ImportReference(typeof(Environment).GetMethod(nameof(Environment.GetEnvironmentVariable), [typeof(string)])
                                                                 ?? throw new InvalidOperationException("System.Environment.GetEnvironmentVariable(string) could not be resolved."));
        il.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Ldstr, "__DD_CALLTARGET_AOT_ROOT_TYPED_ASYNC"));
        il.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Call, getEnvironmentVariableMethod));
        il.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Brfalse_S, skipInstruction));
        il.InsertBefore(firstInstruction, Instruction.Create(OpCodes.Call, typedAsyncRootMethod));
        il.InsertBefore(firstInstruction, skipInstruction);
    }

    /// <summary>
    /// Emits the default value for the supplied type onto the evaluation stack.
    /// </summary>
    private static void EmitDefaultValue(ILProcessor il, MethodDefinition ownerMethod, TypeReference typeReference, ModuleDefinition module)
    {
        if (typeReference is ByReferenceType byReferenceType)
        {
            var elementType = module.ImportReference(byReferenceType.ElementType);
            var byReferenceLocal = new VariableDefinition(elementType);
            ownerMethod.Body.Variables.Add(byReferenceLocal);
            ownerMethod.Body.InitLocals = true;
            il.Append(Instruction.Create(OpCodes.Ldloca, byReferenceLocal));
            il.Append(Instruction.Create(OpCodes.Initobj, elementType));
            il.Append(Instruction.Create(OpCodes.Ldloca, byReferenceLocal));
            return;
        }

        if (!typeReference.IsValueType)
        {
            il.Append(Instruction.Create(OpCodes.Ldnull));
            return;
        }

        var importedType = module.ImportReference(typeReference);
        var local = new VariableDefinition(importedType);
        ownerMethod.Body.Variables.Add(local);
        ownerMethod.Body.InitLocals = true;
        il.Append(Instruction.Create(OpCodes.Ldloca, local));
        il.Append(Instruction.Create(OpCodes.Initobj, importedType));
        il.Append(Instruction.Create(OpCodes.Ldloc, local));
    }

    /// <summary>
    /// Creates the generated proxy instance using the first instance constructor, matching the runtime mapper's
    /// object-to-proxy construction semantics for duck-typed CallTarget values.
    /// </summary>
    private static void EmitCreateNewProxyInstance(ILProcessor il, ModuleDefinition module, TypeDefinition proxyType, TypeReference sourceType)
    {
        var proxyConstructor = proxyType.Methods.FirstOrDefault(method => method.IsConstructor && !method.IsStatic)
                               ?? throw new InvalidOperationException($"The generated DuckType proxy '{proxyType.FullName}' does not expose an instance constructor.");
        if (proxyConstructor.Parameters.Count != 1)
        {
            throw new InvalidOperationException($"The generated DuckType proxy '{proxyType.FullName}' must expose a single-argument instance constructor.");
        }

        if (sourceType.IsValueType && !proxyConstructor.Parameters[0].ParameterType.IsValueType)
        {
            il.Append(Instruction.Create(OpCodes.Box, module.ImportReference(sourceType)));
        }

        il.Append(Instruction.Create(OpCodes.Newobj, module.ImportReference(proxyConstructor)));
    }

    /// <summary>
    /// Creates the emitted DuckType context used to resolve generated proxy and bootstrap types during CallTarget
    /// registry generation.
    /// </summary>
    private static CallTargetAotDuckTypeEmitterContext CreateDuckTypeContext(
        ModuleDefinition module,
        ModuleDefinition duckTypeModule,
        CallTargetAotDuckTypeGenerationResult duckTypeGenerationResult)
    {
        var bootstrapType = ResolveType(duckTypeModule, duckTypeGenerationResult.Dependency.RegistryBootstrapTypeName)
                            ?? throw new InvalidOperationException($"The generated DuckType bootstrap type '{duckTypeGenerationResult.Dependency.RegistryBootstrapTypeName}' could not be resolved from '{duckTypeGenerationResult.Dependency.RegistryAssemblyPath}'.");
        var bootstrapInitializeMethod = bootstrapType.Methods.FirstOrDefault(method => string.Equals(method.Name, "Initialize", StringComparison.Ordinal) && method.Parameters.Count == 0)
                                        ?? throw new InvalidOperationException($"The generated DuckType bootstrap type '{bootstrapType.FullName}' does not expose a public Initialize() method.");
        var proxyTypesByMappingKey = new Dictionary<string, ResolvedDuckTypeProxyType>(StringComparer.Ordinal);
        foreach (var pair in duckTypeGenerationResult.ProxyTypesByMappingKey)
        {
            var proxyType = ResolveType(duckTypeModule, pair.Value.GeneratedProxyTypeName)
                            ?? throw new InvalidOperationException($"The generated DuckType proxy '{pair.Value.GeneratedProxyTypeName}' could not be resolved from '{duckTypeGenerationResult.Dependency.RegistryAssemblyPath}'.");
            proxyTypesByMappingKey[pair.Key] = new ResolvedDuckTypeProxyType(proxyType, module.ImportReference(proxyType));
        }

        return new CallTargetAotDuckTypeEmitterContext(bootstrapType, bootstrapInitializeMethod, proxyTypesByMappingKey);
    }

    /// <summary>
    /// Resolves the generated DuckType proxy types required by a matched CallTarget binding.
    /// </summary>
    private static ResolvedDuckTypeBindings ResolveDuckTypeBindings(
        ModuleDefinition module,
        CallTargetAotMatchedDefinition match,
        CallTargetAotDuckTypeEmitterContext? duckTypeContext)
    {
        if (duckTypeContext is null)
        {
            return ResolvedDuckTypeBindings.Empty(match.ParameterTypeNames.Count);
        }

        var resolvedDuckTypeContext = duckTypeContext.Value;

        var parameterProxyTypes = new List<ResolvedDuckTypeProxyType?>(match.ParameterTypeNames.Count);
        for (var parameterIndex = 0; parameterIndex < match.ParameterTypeNames.Count; parameterIndex++)
        {
            parameterProxyTypes.Add(match.DuckParameterMappingKeys.Count > parameterIndex
                                        ? ResolveDuckTypeProxyType(resolvedDuckTypeContext, match.DuckParameterMappingKeys[parameterIndex])
                                        : null);
        }

        return new ResolvedDuckTypeBindings(
            ResolveDuckTypeProxyType(resolvedDuckTypeContext, match.DuckInstanceMappingKey),
            parameterProxyTypes,
            ResolveDuckTypeProxyType(resolvedDuckTypeContext, match.DuckReturnMappingKey),
            ResolveDuckTypeProxyType(resolvedDuckTypeContext, match.DuckAsyncResultMappingKey));
    }

    /// <summary>
    /// Resolves the generated DuckType proxy type for the supplied canonical mapping key.
    /// </summary>
    private static ResolvedDuckTypeProxyType? ResolveDuckTypeProxyType(CallTargetAotDuckTypeEmitterContext context, string? mappingKey)
    {
        if (string.IsNullOrWhiteSpace(mappingKey))
        {
            return null;
        }

        var resolvedMappingKey = mappingKey!;
        if (context.ProxyTypesByMappingKey.TryGetValue(resolvedMappingKey, out var proxyType))
        {
            return proxyType;
        }

        throw new InvalidOperationException($"The generated DuckType proxy for mapping '{resolvedMappingKey}' could not be resolved.");
    }

    /// <summary>
    /// Creates the assembly resolver used while importing tracer and target metadata into the generated registry.
    /// </summary>
    /// <param name="manifest">The manifest that describes the target and tracer inputs.</param>
    /// <returns>The configured resolver.</returns>
    private static DefaultAssemblyResolver CreateResolver(CallTargetAotManifest manifest, CallTargetAotDuckTypeGenerationResult? duckTypeGenerationResult)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(manifest.TracerAssemblyPath)) ?? Directory.GetCurrentDirectory());
        foreach (var directory in manifest.MatchedDefinitions.Select(static definition => Path.GetDirectoryName(Path.GetFullPath(definition.TargetAssemblyPath))).Where(static path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            resolver.AddSearchDirectory(directory!);
        }

        if (duckTypeGenerationResult is not null)
        {
            resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(duckTypeGenerationResult.Dependency.RegistryAssemblyPath)) ?? Directory.GetCurrentDirectory());
        }

        return resolver;
    }

    /// <summary>
    /// Resolves a type by full name from the supplied module and any forwarded exported types.
    /// </summary>
    /// <param name="module">The module to search.</param>
    /// <param name="fullName">The fully qualified type name.</param>
    /// <returns>The resolved type, if found.</returns>
    private static TypeDefinition? ResolveType(ModuleDefinition module, string fullName)
    {
        var typeDefinition = module.Types.FirstOrDefault(type => string.Equals(type.FullName, fullName, StringComparison.Ordinal));
        if (typeDefinition is not null)
        {
            return typeDefinition;
        }

        return module.ExportedTypes.FirstOrDefault(type => string.Equals(type.FullName, fullName, StringComparison.Ordinal))?.Resolve();
    }

    /// <summary>
    /// Resolves the concrete target method represented by a matched definition.
    /// </summary>
    /// <param name="targetType">The target type that owns the matched method.</param>
    /// <param name="match">The matched definition to resolve.</param>
    /// <returns>The resolved target method, if found.</returns>
    private static MethodDefinition? ResolveMatchedMethod(TypeDefinition targetType, CallTargetAotMatchedDefinition match)
    {
        return targetType.Methods.FirstOrDefault(method =>
            string.Equals(method.Name, match.TargetMethodName, StringComparison.Ordinal) &&
            string.Equals(method.ReturnType.FullName, match.ReturnTypeName, StringComparison.Ordinal) &&
            method.Parameters.Count == match.ParameterTypeNames.Count &&
            method.Parameters.Select(static parameter => parameter.ParameterType.FullName).SequenceEqual(match.ParameterTypeNames, StringComparer.Ordinal));
    }

    /// <summary>
    /// Creates a core library type reference scoped to the target assembly's System.Runtime reference.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="targetModule">The target module whose runtime identity should be matched.</param>
    /// <param name="typeName">The system type name.</param>
    /// <returns>The imported system type reference.</returns>
    private static TypeReference CreateTargetSystemTypeReference(ModuleDefinition module, ModuleDefinition targetModule, string typeName)
    {
        return CreateTargetRuntimeTypeReference(module, targetModule, "System", typeName);
    }

    /// <summary>
    /// Creates a type reference scoped to the target assembly's System.Runtime reference.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="targetModule">The target module whose runtime identity should be matched.</param>
    /// <param name="typeNamespace">The type namespace.</param>
    /// <param name="typeName">The type name.</param>
    /// <returns>The imported system-runtime-scoped type reference.</returns>
    private static TypeReference CreateTargetRuntimeTypeReference(ModuleDefinition module, ModuleDefinition targetModule, string typeNamespace, string typeName)
    {
        var systemRuntimeReference = targetModule.AssemblyReferences.FirstOrDefault(static reference => string.Equals(reference.Name, "System.Runtime", StringComparison.Ordinal));
        if (systemRuntimeReference is null)
        {
            throw new InvalidOperationException($"The target module '{targetModule.FileName}' does not reference System.Runtime.");
        }

        var importedSystemRuntimeReference = EnsureAssemblyReference(module, systemRuntimeReference);
        return new TypeReference(typeNamespace, typeName, module, importedSystemRuntimeReference);
    }

    /// <summary>
    /// Rewrites the generated assembly's core-library references so the registry matches the target application's CoreCLR runtime identity.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="bootstrapType">The generated bootstrap type.</param>
    /// <param name="targetAssemblies">The target assemblies participating in the current generation run.</param>
    private static void RetargetCoreLibraryReferences(ModuleDefinition module, TypeDefinition bootstrapType, IEnumerable<AssemblyDefinition> targetAssemblies)
    {
        var systemRuntimeReference = targetAssemblies
                                    .SelectMany(static assembly => assembly.MainModule.AssemblyReferences)
                                    .FirstOrDefault(static reference => string.Equals(reference.Name, "System.Runtime", StringComparison.Ordinal));
        if (systemRuntimeReference is null)
        {
            return;
        }

        var importedSystemRuntimeReference = EnsureAssemblyReference(module, systemRuntimeReference);
        foreach (var typeReference in module.GetTypeReferences())
        {
            NormalizeTypeReference(typeReference, importedSystemRuntimeReference);
        }

        if (bootstrapType.BaseType.Scope is AssemblyNameReference bootstrapBaseAssemblyReference &&
            string.Equals(bootstrapBaseAssemblyReference.Name, "mscorlib", StringComparison.Ordinal))
        {
            bootstrapType.BaseType = new TypeReference("System", "Object", module, importedSystemRuntimeReference);
        }

        foreach (var memberReference in module.GetMemberReferences())
        {
            switch (memberReference)
            {
                case MethodReference methodReference:
                    NormalizeTypeReference(methodReference.DeclaringType, importedSystemRuntimeReference);
                    NormalizeTypeReference(methodReference.ReturnType, importedSystemRuntimeReference);
                    foreach (var parameter in methodReference.Parameters)
                    {
                        NormalizeTypeReference(parameter.ParameterType, importedSystemRuntimeReference);
                    }

                    break;
                case FieldReference fieldReference:
                    NormalizeTypeReference(fieldReference.DeclaringType, importedSystemRuntimeReference);
                    NormalizeTypeReference(fieldReference.FieldType, importedSystemRuntimeReference);
                    break;
            }
        }

        foreach (var type in module.Types)
        {
            NormalizeTypeReference(type.BaseType, importedSystemRuntimeReference);
            foreach (var field in type.Fields)
            {
                NormalizeTypeReference(field.FieldType, importedSystemRuntimeReference);
            }

            foreach (var method in type.Methods)
            {
                NormalizeTypeReference(method.ReturnType, importedSystemRuntimeReference);
                foreach (var parameter in method.Parameters)
                {
                    NormalizeTypeReference(parameter.ParameterType, importedSystemRuntimeReference);
                }

                foreach (var variable in method.Body.Variables)
                {
                    NormalizeTypeReference(variable.VariableType, importedSystemRuntimeReference);
                }
            }
        }

        var mscorlibReference = module.AssemblyReferences.FirstOrDefault(static reference => string.Equals(reference.Name, "mscorlib", StringComparison.Ordinal));
        if (mscorlibReference is not null)
        {
            module.AssemblyReferences.Remove(mscorlibReference);
        }

        var duplicateSystemRuntimeReferences = module.AssemblyReferences
                                                    .Where(static reference => string.Equals(reference.Name, "System.Runtime", StringComparison.Ordinal))
                                                    .Where(reference => !ReferenceEquals(reference, importedSystemRuntimeReference))
                                                    .ToList();
        foreach (var duplicateReference in duplicateSystemRuntimeReferences)
        {
            module.AssemblyReferences.Remove(duplicateReference);
        }
    }

    /// <summary>
    /// Adds an assembly reference to the generated module when it is not already present.
    /// </summary>
    /// <param name="module">The generated module.</param>
    /// <param name="sourceReference">The source assembly reference to clone.</param>
    /// <returns>The matching assembly reference owned by the generated module.</returns>
    private static AssemblyNameReference EnsureAssemblyReference(ModuleDefinition module, AssemblyNameReference sourceReference)
    {
        var existingReference = module.AssemblyReferences.FirstOrDefault(reference => string.Equals(reference.Name, sourceReference.Name, StringComparison.Ordinal));
        if (existingReference is not null)
        {
            existingReference.Version = sourceReference.Version;
            existingReference.Culture = sourceReference.Culture;
            existingReference.Hash = sourceReference.Hash;
            existingReference.HashAlgorithm = sourceReference.HashAlgorithm;
            existingReference.IsRetargetable = sourceReference.IsRetargetable;
            existingReference.IsSideBySideCompatible = sourceReference.IsSideBySideCompatible;
            existingReference.PublicKey = sourceReference.PublicKey;
            existingReference.PublicKeyToken = sourceReference.PublicKeyToken;
            return existingReference;
        }

        var clonedReference = new AssemblyNameReference(sourceReference.Name, sourceReference.Version)
        {
            Culture = sourceReference.Culture,
            Hash = sourceReference.Hash,
            HashAlgorithm = sourceReference.HashAlgorithm,
            IsRetargetable = sourceReference.IsRetargetable,
            IsSideBySideCompatible = sourceReference.IsSideBySideCompatible,
            PublicKey = sourceReference.PublicKey,
            PublicKeyToken = sourceReference.PublicKeyToken,
        };

        module.AssemblyReferences.Add(clonedReference);
        return clonedReference;
    }

    /// <summary>
    /// Normalizes any core-library-scoped type reference to the canonical target System.Runtime reference.
    /// </summary>
    /// <param name="typeReference">The type reference to normalize.</param>
    /// <param name="systemRuntimeReference">The canonical System.Runtime assembly reference.</param>
    private static void NormalizeTypeReference(TypeReference? typeReference, AssemblyNameReference systemRuntimeReference)
    {
        if (typeReference is null)
        {
            return;
        }

        if (typeReference is not TypeSpecification &&
            (string.Equals(typeReference.Namespace, "System", StringComparison.Ordinal) ||
             IsTargetRuntimeTaskType(typeReference)) &&
            (typeReference.Scope is null ||
             typeReference.Scope is ModuleDefinition ||
             typeReference.Scope is ModuleReference ||
             (typeReference.Scope is AssemblyNameReference assemblyReference &&
              (string.Equals(assemblyReference.Name, "mscorlib", StringComparison.Ordinal) ||
               string.Equals(assemblyReference.Name, "System.Runtime", StringComparison.Ordinal) ||
               string.IsNullOrEmpty(assemblyReference.Name)))))
        {
            typeReference.Scope = systemRuntimeReference;
        }

        switch (typeReference)
        {
            case GenericInstanceType genericInstanceType:
                NormalizeTypeReference(genericInstanceType.ElementType, systemRuntimeReference);
                foreach (var genericArgument in genericInstanceType.GenericArguments)
                {
                    NormalizeTypeReference(genericArgument, systemRuntimeReference);
                }

                break;
            case TypeSpecification typeSpecification:
                NormalizeTypeReference(typeSpecification.ElementType, systemRuntimeReference);
                break;
        }
    }

    /// <summary>
    /// Normalizes type and method names before using them in emitted method identifiers.
    /// </summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>The normalized identifier segment.</returns>
    private static string SanitizeName(string value)
    {
        var chars = value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray();
        return new string(chars);
    }

    /// <summary>
    /// Carries the optional emitted async adapter and the metadata needed to register it.
    /// </summary>
    /// <param name="method">The optional generated async adapter method.</param>
    /// <param name="resultTypeReference">The optional imported async result type reference.</param>
    /// <param name="preserveContext">Whether the callback must preserve the ambient synchronization context.</param>
    /// <param name="isAsyncCallback">Whether the generated callback returns a task.</param>
    private readonly record struct GeneratedAsyncAdapter(
        MethodDefinition? Method,
        TypeReference? ResultTypeReference,
        bool PreserveContext,
        bool IsAsyncCallback);

    /// <summary>
    /// Carries the emitted adapters and imported type references needed by bootstrap registration.
    /// </summary>
    /// <param name="integrationTypeReference">The imported integration type reference.</param>
    /// <param name="targetTypeReference">The imported target type reference.</param>
    /// <param name="returnTypeReference">The optional imported target return type reference.</param>
    /// <param name="argumentTypeReferences">The imported target argument type references.</param>
    /// <param name="asyncResultTypeReference">The optional imported async continuation result type reference.</param>
    /// <param name="beginMethod">The generated begin adapter method.</param>
    /// <param name="endMethod">The generated end adapter method.</param>
    /// <param name="asyncMethod">The optional generated async-end adapter method.</param>
    /// <param name="asyncTaskResultContinuationMethod">The optional generated typed task-return continuation wrapper method.</param>
    /// <param name="asyncTaskResultHelperRootMethod">The optional helper-rooting method for typed task-return adapters.</param>
    /// <param name="asyncPreserveContext">Whether the generated async callback must preserve the ambient synchronization context.</param>
    /// <param name="asyncIsAsyncCallback">Whether the generated async callback returns a task.</param>
    private readonly record struct GeneratedAdapterSet(
        TypeReference IntegrationTypeReference,
        TypeReference TargetTypeReference,
        TypeReference? ReturnTypeReference,
        IReadOnlyList<TypeReference> ArgumentTypeReferences,
        TypeReference? AsyncResultTypeReference,
        MethodDefinition BeginMethod,
        MethodDefinition EndMethod,
        MethodDefinition? AsyncMethod,
        MethodDefinition? AsyncTaskResultContinuationMethod,
        MethodDefinition? AsyncTaskResultHelperRootMethod,
        bool AsyncPreserveContext,
        bool AsyncIsAsyncCallback);

    /// <summary>
    /// Carries the generated DuckType bootstrap method and proxy types resolved from the dependent DuckType registry.
    /// </summary>
    /// <param name="BootstrapType">The generated DuckType bootstrap type.</param>
    /// <param name="BootstrapInitializeMethod">The generated DuckType bootstrap initialize method.</param>
    /// <param name="ProxyTypesByMappingKey">The generated proxy types keyed by canonical mapping key.</param>
    private readonly record struct CallTargetAotDuckTypeEmitterContext(
        TypeDefinition BootstrapType,
        MethodDefinition BootstrapInitializeMethod,
        IReadOnlyDictionary<string, ResolvedDuckTypeProxyType> ProxyTypesByMappingKey);

    /// <summary>
    /// Carries the generated DuckType proxy type resolved for a canonical mapping.
    /// </summary>
    /// <param name="TypeDefinition">The generated proxy type definition.</param>
    /// <param name="TypeReference">The imported generated proxy type reference.</param>
    private readonly record struct ResolvedDuckTypeProxyType(
        TypeDefinition TypeDefinition,
        TypeReference TypeReference);

    /// <summary>
    /// Carries the generated DuckType proxy types required by a matched CallTarget binding.
    /// </summary>
    /// <param name="InstanceProxyType">The optional generated proxy used for the target instance.</param>
    /// <param name="ParameterProxyTypes">The optional generated proxies used for each target parameter.</param>
    /// <param name="ReturnProxyType">The optional generated proxy used for the target method return value.</param>
    /// <param name="AsyncResultProxyType">The optional generated proxy used for the completed async result value.</param>
    private readonly record struct ResolvedDuckTypeBindings(
        ResolvedDuckTypeProxyType? InstanceProxyType,
        IReadOnlyList<ResolvedDuckTypeProxyType?> ParameterProxyTypes,
        ResolvedDuckTypeProxyType? ReturnProxyType,
        ResolvedDuckTypeProxyType? AsyncResultProxyType)
    {
        /// <summary>
        /// Gets the imported generated proxy type reference used for the target instance.
        /// </summary>
        internal TypeReference? InstanceProxyTypeReference => InstanceProxyType?.TypeReference;

        /// <summary>
        /// Gets the generated proxy type definition used for the target instance.
        /// </summary>
        internal TypeDefinition? InstanceProxyTypeDefinition => InstanceProxyType?.TypeDefinition;

        /// <summary>
        /// Gets the imported generated proxy type reference used for the target method return value.
        /// </summary>
        internal TypeReference? ReturnProxyTypeReference => ReturnProxyType?.TypeReference;

        /// <summary>
        /// Gets the generated proxy type definition used for the target method return value.
        /// </summary>
        internal TypeDefinition? ReturnProxyTypeDefinition => ReturnProxyType?.TypeDefinition;

        /// <summary>
        /// Gets the imported generated proxy type reference used for the completed async result.
        /// </summary>
        internal TypeReference? AsyncResultProxyTypeReference => AsyncResultProxyType?.TypeReference;

        /// <summary>
        /// Gets the generated proxy type definition used for the completed async result.
        /// </summary>
        internal TypeDefinition? AsyncResultProxyTypeDefinition => AsyncResultProxyType?.TypeDefinition;

        /// <summary>
        /// Gets the imported generated proxy type reference for the supplied target parameter index.
        /// </summary>
        internal TypeReference? GetParameterProxyType(int parameterIndex) => ParameterProxyTypes[parameterIndex]?.TypeReference;

        /// <summary>
        /// Gets the generated proxy type definition for the supplied target parameter index.
        /// </summary>
        internal TypeDefinition? GetParameterProxyTypeDefinition(int parameterIndex) => ParameterProxyTypes[parameterIndex]?.TypeDefinition;

        /// <summary>
        /// Creates an empty binding set when the selected CallTarget binding does not require DuckType support.
        /// </summary>
        internal static ResolvedDuckTypeBindings Empty(int parameterCount)
        {
            return new ResolvedDuckTypeBindings(null, Enumerable.Repeat<ResolvedDuckTypeProxyType?>(null, parameterCount).ToList(), null, null);
        }
    }
}
