// <copyright file="DuckTypeAotRegistryAssemblyEmitter.Emission.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.DuckTyping;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Proxy emission: emits the IL of the proxy types (forward, reverse and [DuckCopy] proxies, their methods, conversions,
    /// IDuckType implementation and activators), and the registrations that replay the failures dynamic duck typing has for
    /// a mapping.
    /// </summary>
    internal static partial class DuckTypeAotRegistryAssemblyEmitter
    {
        /// <summary>
        /// Diagnostic code of the warning for a compatible mapping that targets, or whose proxy binds, a non-public type or
        /// member of the core library (see DuckTypeAotMappingEmissionResult.RuntimeSpecific).
        /// </summary>
        private const string StatusCodeRuntimeSpecific = "DTAOT0216";

        /// <summary>
        /// Returns the bootstrap method of a registration. Each registration has its own method, called under a handler for type
        /// load failures (see EmitBootstrapRegistrationChunks).
        /// </summary>
        /// <param name="module">The registry module.</param>
        /// <param name="bootstrap">The bootstrap type.</param>
        /// <param name="registrationMethods">The registration methods, by registration index.</param>
        /// <param name="registrationIndex">The index of the registration.</param>
        /// <returns>The method receiving the registration.</returns>
        private static MethodDef GetBootstrapRegistrationMethod(ModuleDef module, TypeDef bootstrap, IList<MethodDef> registrationMethods, int registrationIndex)
        {
            while (registrationMethods.Count <= registrationIndex)
            {
                var method = new MethodDefUser(
                    $"RegisterMapping_{registrationMethods.Count + 1:D5}",
                    MethodSig.CreateStatic(module.CorLibTypes.Void),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig)
                {
                    Body = new CilBody()
                };
                bootstrap.Methods.Add(method);
                registrationMethods.Add(method);
            }

            return registrationMethods[registrationIndex];
        }

        /// <summary>
        /// Defines the parameters of a generated proxy method like dynamic duck typing does for the methods (not the accessors) of a
        /// proxy: the names and attributes (in, out, optional) of the parameters of the method it implements or overrides, and
        /// their default values the runtime stores as constants (see DuckType's InitialiseProxyMethod). A proxy of the proxy binds
        /// them, e.g. an out parameter.
        /// </summary>
        /// <param name="definitionMethod">The method of the proxy definition (or contract) the generated method implements.</param>
        /// <param name="generatedMethod">The generated method.</param>
        private static void CopyProxyMethodParameters(MethodDef definitionMethod, MethodDef generatedMethod)
        {
            var parameterCount = generatedMethod.MethodSig.Params.Count;
            foreach (var definitionParameter in definitionMethod.ParamDefs.OrderBy(parameter => parameter.Sequence))
            {
                if (definitionParameter.Sequence == 0 || definitionParameter.Sequence > parameterCount)
                {
                    continue;
                }

                var attributes = definitionParameter.Attributes & (ParamAttributes.In | ParamAttributes.Out | ParamAttributes.Lcid | ParamAttributes.Retval | ParamAttributes.Optional);
                var parameter = new ParamDefUser(definitionParameter.Name, definitionParameter.Sequence, attributes);
                if (GetStoredDefaultValue(definitionParameter, definitionMethod.MethodSig.Params[definitionParameter.Sequence - 1]) is { } constant)
                {
                    parameter.Constant = constant;
                    parameter.Attributes |= ParamAttributes.HasDefault;
                }

                generatedMethod.ParamDefs.Add(parameter);
            }

            static Constant? GetStoredDefaultValue(ParamDef definitionParameter, TypeSig parameterType)
            {
                parameterType = parameterType.RemovePinnedAndModifiers();
                if (parameterType is ByRefSig byRefSig)
                {
                    parameterType = byRefSig.Next.RemovePinnedAndModifiers();
                }

                // What Reflection.Emit stores: a DateTime as its ticks; not a decimal or a native integer, nor null for a value
                // type other than Nullable<T> (rejected by .NET Framework).
                if (definitionParameter.CustomAttributes.Find("System.Runtime.CompilerServices.DateTimeConstantAttribute") is { ConstructorArguments.Count: 1 } dateTimeConstant &&
                    dateTimeConstant.ConstructorArguments[0].Value is long ticks)
                {
                    return new ConstantUser(ticks, ElementType.I8);
                }

                if (definitionParameter.Constant is not { } constant ||
                    parameterType.FullName is "System.IntPtr" or "System.UIntPtr" ||
                    (constant.Value is null && parameterType.IsValueType && !(parameterType is GenericInstSig genericInstance && genericInstance.GenericType.FullName == "System.Nullable`1")))
                {
                    return null;
                }

                return new ConstantUser(constant.Value, constant.Type);
            }
        }

        /// <summary>
        /// Emits a known failure registration when an incompatible mapping should preserve dynamic exception behavior in AOT mode.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetTypeDefinition">The target type definition.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="failure">The failure value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="emissionWarnings">The emission warnings value.</param>
        /// <returns>true when a failure registration was emitted; otherwise, false.</returns>
        private static bool TryEmitKnownFailureRegistration(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            TypeDef proxyType,
            TypeDef targetTypeDefinition,
            ITypeDefOrRef targetType,
            DuckTypeAotMappingEmissionResult failure,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string>? emissionWarnings = null,
            ITypeDefOrRef? closedProxyType = null,
            IReadOnlyList<KeyValuePair<string, string>>? predictedInnerException = null)
        {
            var phaseStopwatch = StartProfilePhase();
            // Dynamic duck typing in the generator can't read the duck attributes standalone contracts declare themselves.
            var dynamicEngineApplies = !UsesMetadataOnlyDuckAttributes(mapping, proxyType, targetTypeDefinition);
            if (!TryResolveFailureReplay(
                    moduleDef,
                    mapping,
                    failure,
                    dynamicEngineApplies,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    out var resolvedExceptionTypeName,
                    out var resolvedFailureMessage,
                    out var fromDynamicEngine))
            {
                return false;
            }

            if (fromDynamicEngine && _currentExecutionContext?.UnverifiedDynamicFailures.Contains(mapping.Key) != true)
            {
                _currentExecutionContext?.MarkDynamicFailureReplayed(mapping.Key);
            }

            // Closed generic mappings are looked up with their closed proxy type, not the generic type definition.
            var importedProxyType = closedProxyType ?? ImportTypeDefOrRefCached(moduleDef, proxyType, $"failure registration proxy type '{proxyType.FullName}'");
            var importedTargetType = ImportTypeDefOrRefCached(moduleDef, targetType, $"failure registration target type '{targetType.FullName}'");
            var innerException = fromDynamicEngine
                                     ? _currentExecutionContext?.DynamicFailureInnerExceptions.TryGetValue(mapping.Key, out var dynamicInnerException) == true ? dynamicInnerException : null
                                     : predictedInnerException;

            EmitFailureRegistration(
                moduleDef,
                bootstrapType,
                initializeMethod,
                importedMembers,
                mapping.Mode,
                mappingIndex,
                importedProxyType,
                importedTargetType,
                resolvedExceptionTypeName,
                GetFailureReplayDetail(mapping, importedProxyType, importedTargetType, resolvedFailureMessage ?? failure.Detail),
                innerException);

            if (emissionWarnings is not null)
            {
                var diagnosticCode = StringUtil.IsNullOrWhiteSpace(failure.DiagnosticCode) ? "n/a" : failure.DiagnosticCode!;
                var detail = StringUtil.IsNullOrWhiteSpace(failure.Detail) ? "No additional details." : failure.Detail!;
                emissionWarnings.Add(
                    $"Registered AOT failure mapping '{mapping.Key}' to throw '{resolvedExceptionTypeName}' ({diagnosticCode}): {detail}");
            }

            StopProfilePhase(
                phaseStopwatch,
                seconds =>
                {
                    _currentProfile!.KnownFailureRegistrationSeconds += seconds;
                    _currentProfile!.KnownFailureRegistrationCount++;
                });
            return true;
        }

        private static bool TryResolveFailureReplay(
            ModuleDef moduleDef,
            DuckTypeAotMapping mapping,
            DuckTypeAotMappingEmissionResult failure,
            bool dynamicEngineApplies,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            out string failureTypeName,
            out string failureMessage,
            out bool fromDynamicEngine)
        {
            failureTypeName = string.Empty;
            failureMessage = failure.Detail ?? string.Empty;
            fromDynamicEngine = false;

            // The dynamic engine's own failure first: it gives the exception type and message dynamic duck typing throws,
            // in its order (e.g. properties before methods). The classifiers below only cover the mappings dynamic duck typing
            // creates (failures of the AOT emitter), or can't evaluate in the generator.
            if (dynamicEngineApplies &&
                TryResolveDynamicFailureExceptionType(
                    moduleDef,
                    mapping,
                    failure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    out var dynamicFailureExceptionType,
                    out var dynamicFailureMessage))
            {
                failureTypeName = dynamicFailureExceptionType!.FullName ?? dynamicFailureExceptionType.Name;
                failureMessage = dynamicFailureMessage ?? failureMessage;
                fromDynamicEngine = true;
                if (_currentProfile is not null)
                {
                    _currentProfile.FailureClassifierFallbackCount++;
                }

                return true;
            }

            if (TryResolveStaticExactFailureTypeName(failure, out var staticFailureTypeName))
            {
                failureTypeName = staticFailureTypeName!;
                if (_currentProfile is not null)
                {
                    _currentProfile.FailureClassifierFastPathCount++;
                }

                return true;
            }

            if (TryResolveBroadFailureTypeName(failure, out var broadFailureTypeName))
            {
                failureTypeName = broadFailureTypeName!;
                if (_currentProfile is not null)
                {
                    _currentProfile.FailureClassifierFallbackCount++;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Emits the mapping of an array proxy type: the failure dynamic duck typing has with it (an array type isn't a proxy
        /// definition it can implement), or else an unsupported proxy kind.
        /// </summary>
        /// <param name="moduleDef">The registry module.</param>
        /// <param name="bootstrapType">The bootstrap type.</param>
        /// <param name="initializeMethod">The registration method.</param>
        /// <param name="importedMembers">The imported members.</param>
        /// <param name="mapping">The mapping.</param>
        /// <param name="mappingIndex">The registration index.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name.</param>
        /// <returns>The emission result.</returns>
        private static DuckTypeAotMappingEmissionResult EmitArrayProxyTypeMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName)
        {
            var unsupported = DuckTypeAotMappingEmissionResult.NotCompatible(
                mapping,
                DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                StatusCodeUnsupportedProxyKind,
                $"Proxy type '{mapping.ProxyTypeName}' is an array type, which the registry can't generate a proxy of.");
            if (!proxyAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath) ||
                !targetAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath) ||
                !TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) ||
                !TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType) ||
                proxyRuntimeType is null ||
                targetRuntimeType is null ||
                GetDynamicCreationOutcome(mapping, proxyRuntimeType, targetRuntimeType, out var exceptionType, out var exceptionMessage) != DynamicCreationOutcome.Failed ||
                exceptionType is null)
            {
                return unsupported;
            }

            var importedProxyType = ImportRuntimeTypeCached(moduleDef, proxyRuntimeType, $"array proxy type '{mapping.ProxyTypeName}'");
            var importedTargetType = ImportRuntimeTypeCached(moduleDef, targetRuntimeType, $"target type '{mapping.TargetTypeName}'");
            EmitFailureRegistration(
                moduleDef,
                bootstrapType,
                initializeMethod,
                importedMembers,
                mapping.Mode,
                mappingIndex,
                importedProxyType,
                importedTargetType,
                exceptionType.FullName ?? exceptionType.Name,
                GetFailureReplayDetail(mapping, importedProxyType, importedTargetType, exceptionMessage),
                _currentExecutionContext?.DynamicFailureInnerExceptions.TryGetValue(mapping.Key, out var innerException) == true ? innerException : null);
            _currentExecutionContext?.MarkDynamicFailureReplayed(mapping.Key);
            return DuckTypeAotMappingEmissionResult.NotCompatible(
                mapping,
                DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                StatusCodeUnsupportedProxyKind,
                exceptionMessage ?? unsupported.Detail!);
        }

        /// <summary>
        /// Gets the message replayed by a failure registration. Known DuckType exceptions use it as their whole message,
        /// so it falls back to describing the mapping instead of replaying an empty message.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="detail">The resolved failure detail value.</param>
        /// <returns>The failure message to replay.</returns>
        private static string GetFailureReplayDetail(DuckTypeAotMapping mapping, ITypeDefOrRef proxyType, ITypeDefOrRef targetType, string? detail)
        {
            if (!StringUtil.IsNullOrWhiteSpace(detail))
            {
                return detail!;
            }

            return mapping.Mode == DuckTypeAotMappingMode.Reverse
                       ? $"The AOT reverse proxy deriving from '{proxyType.FullName}' cannot be created for delegation type '{targetType.FullName}'."
                       : $"The AOT proxy for '{proxyType.FullName}' cannot be created for target type '{targetType.FullName}'.";
        }

        /// <summary>
        /// Emits a failure registration entry into the bootstrap method.
        /// </summary>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mode">The mapping mode value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="exceptionType">The exception type value.</param>
        private static void EmitFailureRegistration(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMappingMode mode,
            int mappingIndex,
            ITypeDefOrRef proxyType,
            ITypeDefOrRef targetType,
            string failureTypeName,
            string detail,
            IReadOnlyList<KeyValuePair<string, string>>? innerException = null)
        {
            var failureThrowerMethod = GetOrCreateFailureThrowerMethod(moduleDef, bootstrapType, importedMembers, mappingIndex, failureTypeName, detail, innerException);
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(proxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(targetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            initializeMethod.Body.Instructions.Add(OpCodes.Ldftn.ToInstruction(failureThrowerMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(importedMembers.FuncExceptionCtor));
            initializeMethod.Body.Instructions.Add(
                OpCodes.Call.ToInstruction(
                    mode == DuckTypeAotMappingMode.Reverse
                        ? importedMembers.RegisterAotReverseProxyFailureMethod
                        : importedMembers.RegisterAotProxyFailureMethod));
        }

        /// <summary>
        /// Emits the cast of the instance an activator receives (its first argument) to the target type. An instance of another
        /// type throws the InvalidCastException dynamic duck typing's activator throws (CoreCLR's cast, which names both types,
        /// where NativeAOT's doesn't).
        /// </summary>
        /// <param name="body">The activator body.</param>
        /// <param name="importedMembers">The imported members.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="targetIsValueType">Whether the target type is a value type.</param>
        private static void EmitActivatorInstanceCast(CilBody body, ImportedMembers importedMembers, ITypeDefOrRef targetType, bool targetIsValueType, OpCode? loadInstance = null)
        {
            var load = loadInstance ?? OpCodes.Ldarg_0;
            var cast = load.ToInstruction();
            body.Instructions.Add(load.ToInstruction());
            body.Instructions.Add(OpCodes.Isinst.ToInstruction(targetType));
            body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(cast));
            body.Instructions.Add(load.ToInstruction());
            body.Instructions.Add(OpCodes.Brfalse_S.ToInstruction(cast));
            body.Instructions.Add(load.ToInstruction());
            body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(targetType));
            body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.ThrowActivatorInvalidCastMethod));
            body.Instructions.Add(cast);
            body.Instructions.Add((targetIsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass).ToInstruction(targetType));
        }

        /// <summary>
        /// Emits the typed activator of a registration: a CreateProxyInstance&lt;TProxy&gt; (the delegate type of dynamic duck
        /// typing's activators, which CreateInstance&lt;TProxy&gt; calls directly) to a generated static method, bound to the object
        /// activator (its first argument), which object-based creation calls (see DuckType.CreateTypeResult).
        /// </summary>
        /// <param name="moduleDef">The registry module.</param>
        /// <param name="bootstrapType">The bootstrap type.</param>
        /// <param name="body">The registration method body.</param>
        /// <param name="importedMembers">The imported members.</param>
        /// <param name="mappingIndex">The registration index.</param>
        /// <param name="proxyTypeSig">The proxy definition type (TProxy).</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="targetIsValueType">Whether the target type is a value type.</param>
        /// <param name="activatorMethod">The activator creating the proxy of a target instance.</param>
        /// <param name="objectActivatorMethod">The object activator.</param>
        private static void EmitTypedActivatorDelegate(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            CilBody body,
            ImportedMembers importedMembers,
            int mappingIndex,
            TypeSig proxyTypeSig,
            ITypeDefOrRef targetType,
            bool targetIsValueType,
            MethodDef activatorMethod,
            MethodDef objectActivatorMethod)
        {
            // TProxy ActivateTypedProxy_N(Func<object?, object?> objectActivator, object instance)
            var typedActivatorMethod = new MethodDefUser(
                $"ActivateTypedProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(proxyTypeSig, importedMembers.FuncObjectObjectTypeSig, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            typedActivatorMethod.Body = new CilBody();
            EmitActivatorInstanceCast(typedActivatorMethod.Body, importedMembers, targetType, targetIsValueType, OpCodes.Ldarg_1);
            typedActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            typedActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(typedActivatorMethod);

            var typedActivatorType = new TypeSpecUser(new GenericInstSig((ClassSig)importedMembers.CreateProxyInstanceType.ToTypeSig(), proxyTypeSig));
            EmitFuncObjectObjectDelegate(body, importedMembers, objectActivatorMethod);
            body.Instructions.Add(OpCodes.Ldftn.ToInstruction(typedActivatorMethod));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(new MemberRefUser(moduleDef, ".ctor", MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.IntPtr), typedActivatorType)));
        }

        /// <summary>
        /// Emits a direct <see cref="Func{T, TResult}"/> delegate to a generated static object activator.
        /// </summary>
        /// <param name="body">The target method body.</param>
        /// <param name="importedMembers">The imported member cache.</param>
        /// <param name="method">The generated static method.</param>
        private static void EmitFuncObjectObjectDelegate(CilBody body, ImportedMembers importedMembers, MethodDef method)
        {
            body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            body.Instructions.Add(OpCodes.Ldftn.ToInstruction(method));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(importedMembers.FuncObjectObjectCtor));
        }

        private static MethodDef GetOrCreateFailureThrowerMethod(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            ImportedMembers importedMembers,
            int mappingIndex,
            string failureTypeName,
            string detail,
            IReadOnlyList<KeyValuePair<string, string>>? innerException = null)
        {
            detail ??= string.Empty;
            var failureThrowerCacheKey = string.Concat(
                failureTypeName,
                "::",
                detail,
                "::",
                innerException is null ? string.Empty : string.Join("::", innerException.Select(level => level.Key + "::" + level.Value)));
            if (_currentExecutionContext?.TryGetFailureThrowerMethod(failureThrowerCacheKey, out var cachedFailureThrowerMethod) == true)
            {
                return cachedFailureThrowerMethod;
            }

            var failureThrowerMethod = EmitFailureThrowerMethod(moduleDef, bootstrapType, importedMembers, mappingIndex, failureTypeName, detail, innerException);
            _currentExecutionContext?.CacheFailureThrowerMethod(failureThrowerCacheKey, failureThrowerMethod);
            return failureThrowerMethod;
        }

        /// <summary>
        /// Emits a failure thrower method used to replay deterministic AOT failures.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="failureTypeName">The failure type name value.</param>
        /// <param name="detail">The failure detail value.</param>
        /// <returns>The emitted thrower method.</returns>
        private static MethodDef EmitFailureThrowerMethod(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            ImportedMembers importedMembers,
            int mappingIndex,
            string failureTypeName,
            string detail,
            IReadOnlyList<KeyValuePair<string, string>>? innerException)
        {
            var method = new MethodDefUser(
                $"CreateFailure_{mappingIndex:D4}",
                MethodSig.CreateStatic(RuntimeImporter(moduleDef).Import(typeof(Exception)).ToTypeSig()),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            method.Body = new CilBody();
            method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(failureTypeName));
            method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(detail ?? string.Empty));
            if (innerException is { Count: > 0 } && TryEmitExceptionChain(moduleDef, method.Body, innerException, 0))
            {
                // The exceptions dynamic duck typing's wraps, created with their types (the ones of the core library).
                method.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.DuckTypeAotRegisteredFailureCreateWithInnerExceptionInstanceMethod));
            }
            else if (innerException is { Count: > 0 })
            {
                method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(innerException[0].Key));
                method.Body.Instructions.Add(OpCodes.Ldstr.ToInstruction(innerException[0].Value));
                method.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.DuckTypeAotRegisteredFailureCreateWithInnerExceptionMethod));
            }
            else
            {
                method.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.DuckTypeAotRegisteredFailureCreateMethod));
            }

            method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(method);
            return method;
        }

        /// <summary>
        /// Emits the creation of a chain of wrapped exceptions (each wraps the next one), as far as their types are public types
        /// of the core library with a constructor taking the message (and the inner exception, for the ones that wrap another).
        /// </summary>
        /// <param name="moduleDef">The registry module.</param>
        /// <param name="body">The method body.</param>
        /// <param name="exceptions">The types and messages of the exceptions, from the outermost.</param>
        /// <param name="index">The index of the exception to create.</param>
        /// <returns>true if the exception was created; false if its type can't be created this way (nothing is emitted).</returns>
        private static bool TryEmitExceptionChain(ModuleDef moduleDef, CilBody body, IReadOnlyList<KeyValuePair<string, string>> exceptions, int index)
        {
            if (typeof(object).Assembly.GetType(exceptions[index].Key, throwOnError: false) is not { IsPublic: true } exceptionType ||
                !typeof(Exception).IsAssignableFrom(exceptionType) ||
                exceptionType.GetConstructor([typeof(string)]) is not { } messageConstructor)
            {
                return false;
            }

            body.Instructions.Add(OpCodes.Ldstr.ToInstruction(exceptions[index].Value));
            if (index + 1 < exceptions.Count &&
                exceptionType.GetConstructor([typeof(string), typeof(Exception)]) is { } wrappingConstructor &&
                TryEmitExceptionChain(moduleDef, body, exceptions, index + 1))
            {
                body.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(wrappingConstructor)));
            }
            else
            {
                body.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(messageConstructor)));
            }

            return true;
        }

        /// <summary>
        /// Attempts to resolve the exception dynamic duck typing throws for a mapping (see <see cref="GetDynamicCreationOutcome"/>).
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="failure">The AOT failure of the mapping, if any.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="exceptionType">The resolved exception type value.</param>
        /// <param name="exceptionMessage">The resolved exception message value.</param>
        /// <returns>true when dynamic duck typing fails to create the proxy; otherwise, false.</returns>
        private static bool TryResolveDynamicFailureExceptionType(
            ModuleDef moduleDef,
            DuckTypeAotMapping mapping,
            DuckTypeAotMappingEmissionResult failure,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            out Type? exceptionType,
            out string? exceptionMessage)
            => GetDynamicCreationOutcome(mapping, proxyAssemblyPathsByName, targetAssemblyPathsByName, out exceptionType, out exceptionMessage) == DynamicCreationOutcome.Failed;

        /// <summary>
        /// Attempts to resolve a known failure exception type from an emission result.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="failure">The failure value.</param>
        /// <param name="exceptionType">The exception type value.</param>
        /// <returns>true when a known exception type was resolved; otherwise, false.</returns>
        private static bool TryResolveStaticExactFailureTypeName(
            DuckTypeAotMappingEmissionResult failure,
            out string? exceptionTypeName)
        {
            exceptionTypeName = null;
            var detail = failure.Detail ?? string.Empty;

            if (string.Equals(failure.DiagnosticCode, StatusCodeUnsupportedProxyKind, StringComparison.Ordinal) &&
                detail.IndexOf("Reverse proxy type", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = typeof(DuckTypeReverseProxyBaseIsStructException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodePropertyCantBeWritten, StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypePropertyCantBeWrittenException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeCustomAttributeNamedArguments, StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypeCustomAttributeHasNamedArgumentsException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeUnloadableProxyType, StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypeException).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("cannot be abstract or interface", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = typeof(DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("marked with [DuckReverseMethod]", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = detail.IndexOf("Proxy property", StringComparison.Ordinal) >= 0
                                        ? typeof(DuckTypeIncorrectReversePropertyUsageException).FullName
                                        : typeof(DuckTypeIncorrectReverseMethodUsageException).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("belongs to value type", StringComparison.Ordinal) >= 0 ||
                detail.StartsWith("Modifying struct members is not supported.", StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypeStructMembersCannotBeChangedException).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("does not expose any writable public fields", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = typeof(DuckTypeDuckCopyStructDoesNotContainsAnyField).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeMissingMethod, StringComparison.Ordinal) &&
                detail.StartsWith("The property or field '", StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypePropertyOrFieldNotFoundException).FullName;
                return exceptionTypeName is not null;
            }

            if (detail.IndexOf("Ambiguous target method match", StringComparison.Ordinal) >= 0)
            {
                exceptionTypeName = typeof(DuckTypeTargetMethodAmbiguousMatchException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeIncompatibleSignature, StringComparison.Ordinal) &&
                IsReadonlyFieldFailure(failure.Detail ?? string.Empty))
            {
                exceptionTypeName = typeof(DuckTypeFieldIsReadonlyException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeIncompatibleSignature, StringComparison.Ordinal))
            {
                if (IsParameterSignatureFailure(detail))
                {
                    exceptionTypeName = typeof(DuckTypeProxyAndTargetMethodParameterSignatureMismatchException).FullName;
                    return exceptionTypeName is not null;
                }
            }

            return false;
        }

        private static bool TryResolveBroadFailureTypeName(
            DuckTypeAotMappingEmissionResult failure,
            out string? exceptionTypeName)
        {
            exceptionTypeName = null;
            var detail = failure.Detail ?? string.Empty;

            if (string.Equals(failure.DiagnosticCode, StatusCodeMissingMethod, StringComparison.Ordinal))
            {
                exceptionTypeName = typeof(DuckTypeTargetMethodNotFoundException).FullName;
                return exceptionTypeName is not null;
            }

            if (string.Equals(failure.DiagnosticCode, StatusCodeIncompatibleSignature, StringComparison.Ordinal))
            {
                if (IsReturnTypeFailure(detail))
                {
                    exceptionTypeName = typeof(DuckTypeProxyAndTargetMethodReturnTypeMismatchException).FullName;
                    return exceptionTypeName is not null;
                }

                if (IsInvalidTypeConversionFailure(detail))
                {
                    exceptionTypeName = typeof(DuckTypeInvalidTypeConversionException).FullName;
                    return exceptionTypeName is not null;
                }
            }

            return false;
        }

        /// <summary>
        /// Emits mapping.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="proxyModulesByAssemblyName">The proxy modules by assembly name value.</param>
        /// <param name="targetModulesByAssemblyName">The target modules by assembly name value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="emissionWarnings">The emission warnings value.</param>
        /// <param name="generatedReverseTarget">The generated reverse proxy type the proxy is created for, instead of the target.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotMappingEmissionResult EmitMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string> emissionWarnings,
            TypeDef? generatedReverseTarget = null)
        {
            var isReverseMapping = mapping.Mode == DuckTypeAotMappingMode.Reverse;

            // Array targets ("System.Int32[]", recorded like any other runtime type) and array delegation types of reverse
            // proxies have no type definition: they're bound through their runtime type, with the members of System.Array, which
            // is what dynamic duck typing sees.
            // An array proxy type (e.g. a mapping recorded for the duck chaining of an array member): dynamic duck typing can't
            // create a proxy of it, and the registry replays its failure.
            if (DuckTypeAotNameHelpers.IsArrayTypeName(mapping.ProxyTypeName))
            {
                return EmitArrayProxyTypeMapping(moduleDef, bootstrapType, initializeMethod, importedMembers, mapping, mappingIndex, proxyAssemblyPathsByName, targetAssemblyPathsByName);
            }

            if (DuckTypeAotNameHelpers.IsArrayTypeName(mapping.TargetTypeName))
            {
                return EmitArrayTargetMapping(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyModulesByAssemblyName,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings);
            }

            // Closed-generic mappings use a stricter emission path so their compatibility outcomes stay deterministic.
            if (DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.ProxyTypeName) ||
                DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.TargetTypeName))
            {
                return EmitClosedGenericMapping(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    isReverseMapping,
                    proxyModulesByAssemblyName,
                    targetModulesByAssemblyName,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    generatedReverseTarget);
            }

            // Early resolution failures are emitted as explicit matrix diagnostics instead of partial proxy generation.
            if (!proxyModulesByAssemblyName.TryGetValue(mapping.ProxyAssemblyName, out var proxyModule))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy assembly '{mapping.ProxyAssemblyName}' was not loaded.");
            }

            if (!targetModulesByAssemblyName.TryGetValue(mapping.TargetAssemblyName, out var targetModule))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target assembly '{mapping.TargetAssemblyName}' was not loaded.");
            }

            if (!TryResolveType(proxyModule, mapping.ProxyTypeName, out var proxyType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy type '{mapping.ProxyTypeName}' was not found in '{mapping.ProxyAssemblyName}'.");
            }

            if (!TryResolveType(targetModule, mapping.TargetTypeName, out var targetType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target type '{mapping.TargetTypeName}' was not found in '{mapping.TargetAssemblyName}'.");
            }

            return EmitResolvedTypeMapping(
                moduleDef,
                bootstrapType,
                initializeMethod,
                importedMembers,
                mapping,
                mappingIndex,
                isReverseMapping,
                proxyType,
                targetType,
                // A generated reverse proxy type deriving from (or implementing) the mapped target is the instance instead.
                generatedReverseTarget ?? ImportTypeDefOrRefCached(moduleDef, targetType, $"target type '{targetType.FullName}'"),
                generatedReverseTarget?.ToTypeSig() ?? ImportTypeSigCached(moduleDef, targetType.ToTypeSig(), $"target type signature '{targetType.FullName}'"),
                generatedReverseTarget?.IsValueType ?? targetType.IsValueType,
                closedGenericTargetTypeArguments: null,
                importedProxyContractType: null,
                importedProxyContractTypeSig: null,
                closedGenericProxyTypeArguments: null,
                proxyAssemblyPathsByName,
                targetAssemblyPathsByName,
                emissionWarnings);
        }

        /// <summary>
        /// Emits mapping for already-resolved proxy/target types.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="isReverseMapping">The is reverse mapping value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="importedTargetType">The imported target type value.</param>
        /// <param name="importedTargetTypeSig">The imported target type sig value.</param>
        /// <param name="targetIsValueType">The target is value type value.</param>
        /// <param name="closedGenericTargetTypeArguments">The closed generic target type arguments value.</param>
        /// <param name="importedProxyContractType">The imported closed proxy contract type value.</param>
        /// <param name="importedProxyContractTypeSig">The imported closed proxy contract type signature value.</param>
        /// <param name="closedGenericProxyTypeArguments">The closed generic proxy type arguments value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="emissionWarnings">The emission warnings value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotMappingEmissionResult EmitResolvedTypeMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            bool isReverseMapping,
            TypeDef proxyType,
            TypeDef targetType,
            ITypeDefOrRef importedTargetType,
            TypeSig importedTargetTypeSig,
            bool targetIsValueType,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            ITypeDefOrRef? importedProxyContractType,
            TypeSig? importedProxyContractTypeSig,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string> emissionWarnings)
        {
            // Decided from the types of the mapping, before its members are bound (the member selection reads it).
            _ = UsesMetadataOnlyDuckAttributes(mapping, proxyType, targetType);

            // Value-type proxy definitions are DuckCopy projections and must follow the dedicated struct-copy emitter.
            if (proxyType.IsValueType)
            {
                if (isReverseMapping)
                {
                    var reverseValueTypeFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                        StatusCodeUnsupportedProxyKind,
                        $"Reverse proxy type '{mapping.ProxyTypeName}' is not supported when the proxy definition is a value type.");

                    TryEmitKnownFailureRegistration(
                        moduleDef,
                        bootstrapType,
                        initializeMethod,
                        importedMembers,
                        mapping,
                        mappingIndex,
                        proxyType,
                        targetType,
                        importedTargetType,
                        reverseValueTypeFailure,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName,
                        emissionWarnings,
                        importedProxyContractType);

                    return reverseValueTypeFailure;
                }

                var structCopyResult = EmitStructCopyMapping(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    importedTargetType,
                    importedTargetTypeSig,
                    targetIsValueType,
                    closedGenericTargetTypeArguments,
                    importedProxyContractType,
                    importedProxyContractTypeSig);

                if (!string.Equals(structCopyResult.Status, DuckTypeAotCompatibilityStatuses.Compatible, StringComparison.Ordinal))
                {
                    TryEmitKnownFailureRegistration(
                        moduleDef,
                        bootstrapType,
                        initializeMethod,
                        importedMembers,
                        mapping,
                        mappingIndex,
                        proxyType,
                        targetType,
                        importedTargetType,
                        structCopyResult,
                        proxyAssemblyPathsByName,
                        targetAssemblyPathsByName,
                        emissionWarnings,
                        importedProxyContractType);
                }

                return structCopyResult;
            }

            // Only interface/class proxy definitions are supported here; value-type proxies are handled by DuckCopy flow.
            if (!proxyType.IsInterface && !proxyType.IsClass)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                    StatusCodeUnsupportedProxyKind,
                    $"Proxy type '{mapping.ProxyTypeName}' is not supported. Only interface, class, and DuckCopy struct proxies are emitted in this phase.");
            }

            // An array delegation type is bound against the System.Array definition, which is abstract: array types aren't.
            if (isReverseMapping && (targetType.IsInterface || targetType.IsAbstract) && !DuckTypeAotNameHelpers.IsArrayTypeName(mapping.TargetTypeName))
            {
                var reverseProxyImplementorFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.IncompatibleMethodSignature,
                    StatusCodeIncompatibleSignature,
                    $"Reverse proxy implementor type '{mapping.TargetTypeName}' cannot be abstract or interface.");
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    importedTargetType,
                    reverseProxyImplementorFailure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType);
                return reverseProxyImplementorFailure;
            }

            var isInterfaceProxy = proxyType.IsInterface;
            var isDuckAsClassInterface = isInterfaceProxy && HasDuckAsClassAttribute(proxyType);
            // This branch controls default allocation behavior for all interface mappings:
            // struct by default (parity/perf), class only when explicitly requested via [DuckAsClass].
            var emitInterfaceStructProxy = isInterfaceProxy && !isDuckAsClassInterface;
            var planningStopwatch = StartProfilePhase();
            if (!TryCollectForwardBindings(mapping, proxyType, targetType, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, isInterfaceProxy, out var bindings, out var failure))
            {
                StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    importedTargetType,
                    failure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType);
                return failure!;
            }

            StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);
            RecordRuntimeSpecificBindings(mapping, targetType, bindings.Select(binding => (IMemberDef?)binding.TargetMethod ?? binding.TargetField));

            IMethod? baseCtorToCall = importedMembers.ObjectCtor;
            // Match dynamic proxies: call a parameterless base constructor when one exists.
            if (!isInterfaceProxy)
            {
                var baseConstructor = FindSupportedProxyBaseConstructor(proxyType);
                // Dynamic proxies skip the base call when no parameterless constructor exists.
                baseCtorToCall = baseConstructor is null
                                     ? null
                                     : ImportMethodCached(moduleDef, baseConstructor, $"base constructor for proxy '{proxyType.FullName}'");
            }

            // Keep successful metadata plans subject to the dynamic engine's current validation contract.
            // This catches binder ambiguity and unsafe open generic signatures before IL is emitted.
            var dynamicValidationFailure = ValidateMappingWithDynamicEngine(moduleDef, mapping, proxyType, targetType, proxyAssemblyPathsByName, targetAssemblyPathsByName, out var evaluatedByDynamicEngine);
            if (dynamicValidationFailure is not null)
            {
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    importedTargetType,
                    dynamicValidationFailure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType);
                return dynamicValidationFailure;
            }

            // When dynamic duck typing can't tell (see above), and the generator can't load the proxy type it generates either (see
            // ValidateGeneratedRegistry, which lets the runtime decide), detect from metadata the proxy types that can't be loaded.
            if (!evaluatedByDynamicEngine &&
                !CanLoadMappingTypes(mapping) &&
                TryGetUnloadableProxyTypeFailure(mapping, proxyType, isInterfaceProxy, out var unloadableProxyTypeFailure, out var unloadableProxyTypeReason))
            {
                emissionWarnings.Add($"Proxy type for mapping '{mapping.Key}' can't be created: {unloadableProxyTypeReason}.");
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    importedTargetType,
                    unloadableProxyTypeFailure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType);
                return unloadableProxyTypeFailure!;
            }

            // A proxy type a previous pass generated but the runtime can't load (see ValidateGeneratedRegistry).
            var registrationKey = string.Concat(mapping.Key, "|", importedTargetType.FullName);
            if (unloadableGeneratedProxyTypes?.TryGetValue(registrationKey, out var loadFailure) == true)
            {
                emissionWarnings.Add($"Proxy type for mapping '{mapping.Key}' (target '{importedTargetType.FullName}') can't be loaded: {loadFailure}");
                var unloadableFailure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                    StatusCodeUnloadableProxyType,
                    CreateDuckTypeCreationFailureMessage(importedTargetType.ReflectionFullName, mapping.ProxyTypeName));
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    importedTargetType,
                    unloadableFailure,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType,
                    predictedInnerException: [new KeyValuePair<string, string>(typeof(TypeLoadException).FullName!, loadFailure!)]);
                return unloadableFailure;
            }

            var generatedTypeName = $"DuckTypeProxy_{mappingIndex:D4}_{ComputeStableShortHash(mapping.Key)}";
            var resolvedProxyContractType = importedProxyContractType ?? ImportTypeDefOrRefCached(moduleDef, proxyType, $"proxy contract type '{proxyType.FullName}'");
            var resolvedProxyContractTypeSig = importedProxyContractTypeSig ?? ImportTypeSigCached(moduleDef, proxyType.ToTypeSig(), $"proxy type signature '{proxyType.FullName}'");
            if (!isInterfaceProxy &&
                baseCtorToCall?.DeclaringType is ITypeDefOrRef baseConstructorDeclaringType &&
                !string.Equals(baseConstructorDeclaringType.FullName, resolvedProxyContractType.FullName, StringComparison.Ordinal))
            {
                baseCtorToCall = moduleDef.UpdateRowId(new MemberRefUser(
                    moduleDef,
                    baseCtorToCall.Name,
                    baseCtorToCall.MethodSig,
                    resolvedProxyContractType));
            }

            var generatedParentType = emitInterfaceStructProxy ? moduleDef.CorLibTypes.GetTypeRef("System", "ValueType") : (isInterfaceProxy ? moduleDef.CorLibTypes.Object.TypeDefOrRef : resolvedProxyContractType);
            var generatedTypeAttributes = emitInterfaceStructProxy
                                              ? TypeAttributes.Public | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.Serializable
                                              : TypeAttributes.Public | TypeAttributes.AutoLayout | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed;
            var generatedType = new TypeDefUser(
                GeneratedProxyNamespace,
                generatedTypeName,
                generatedParentType)
            {
                Attributes = generatedTypeAttributes
            };

            // Interface proxies implement the user contract directly.
            if (isInterfaceProxy)
            {
                generatedType.Interfaces.Add(new InterfaceImplUser(resolvedProxyContractType));
            }

            generatedType.Interfaces.Add(new InterfaceImplUser(importedMembers.IDuckTypeType));
            moduleDef.Types.Add(generatedType);

            if (isReverseMapping &&
                !TryApplyReverseTargetCustomAttributes(moduleDef, generatedType, targetType, mapping, out var reverseCustomAttributeFailure))
            {
                TryEmitKnownFailureRegistration(
                    moduleDef,
                    bootstrapType,
                    initializeMethod,
                    importedMembers,
                    mapping,
                    mappingIndex,
                    proxyType,
                    targetType,
                    importedTargetType,
                    reverseCustomAttributeFailure!,
                    proxyAssemblyPathsByName,
                    targetAssemblyPathsByName,
                    emissionWarnings,
                    importedProxyContractType);
                return reverseCustomAttributeFailure!;
            }

            // The proxy of an array type also serves the other array types, and the proxy of a type of the core library its
            // non-public types (see DuckTypeAotEngine.TryGetFallbackResult): it receives the runtime type, which it reports as
            // IDuckType.Type like the proxy dynamic duck typing creates for that type. A proxy of an array type binds the members
            // of System.Array, and stores the instance as one.
            // A [DuckType(IncludeDerivedTypes = true)] declaration serves the classes deriving from its target type the same way.
            var isArrayTarget = !isReverseMapping && IsArrayTargetMapping(mapping);
            var servesDerivedTypes = !isReverseMapping && !isArrayTarget && mapping.IncludesDerivedTypes && !targetType.IsValueType && !targetType.IsSealed;
            var servesFallbackTypes = !isReverseMapping && (isArrayTarget ? !IsSystemArrayTargetMapping(mapping) : HasRuntimeInternalSubtypes(targetType) || servesDerivedTypes);
            var systemArrayType = moduleDef.CorLibTypes.GetTypeRef("System", "Array");
            var instanceSig = isArrayTarget ? new ClassSig(systemArrayType) : importedTargetTypeSig;
            var targetField = new FieldDefUser("_currentInstance", new FieldSig(instanceSig), FieldAttributes.Private | FieldAttributes.InitOnly);
            generatedType.Fields.Add(targetField);
            FieldDef? reportedTargetTypeField = null;
            if (servesFallbackTypes)
            {
                reportedTargetTypeField = new FieldDefUser("_targetType", new FieldSig(importedMembers.SystemTypeSig), FieldAttributes.Private | FieldAttributes.InitOnly);
                generatedType.Fields.Add(reportedTargetTypeField);
            }

            var generatedConstructorSig = reportedTargetTypeField is null
                                              ? MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, instanceSig)
                                              : MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, instanceSig, importedMembers.SystemTypeSig);
            // The constructor that also receives the served runtime type is internal: the public constructor of a proxy takes the
            // instance only, like the one dynamic duck typing creates, which CallTarget's IntegrationMapper (and other emitted
            // code) calls through Type.GetConstructors()[0].
            var generatedConstructor = new MethodDefUser(
                ".ctor",
                generatedConstructorSig,
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                (reportedTargetTypeField is null ? MethodAttributes.Public : MethodAttributes.Assembly) | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            generatedConstructor.Body = new CilBody();

            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            generatedConstructor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(targetField));
            if (reportedTargetTypeField is not null)
            {
                // Null for the registered target type itself (which a caller of the constructor may pass too).
                var storeReportedType = Instruction.Create(OpCodes.Stfld, reportedTargetTypeField);
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_2.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Dup.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
                generatedConstructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
                generatedConstructor.Body.Instructions.Add(OpCodes.Bne_Un_S.ToInstruction(storeReportedType));
                generatedConstructor.Body.Instructions.Add(OpCodes.Pop.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                generatedConstructor.Body.Instructions.Add(storeReportedType);
            }

            // Class proxy base constructors can call virtual members, so store the target first to match dynamic ducktyping.
            if (!emitInterfaceStructProxy && baseCtorToCall is not null)
            {
                generatedConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                generatedConstructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(baseCtorToCall));
            }

            generatedConstructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            generatedType.Methods.Add(generatedConstructor);
            if (reportedTargetTypeField is not null)
            {
                var publicConstructor = new MethodDefUser(
                    ".ctor",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, instanceSig),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                publicConstructor.Body = new CilBody();
                publicConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                publicConstructor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
                publicConstructor.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                publicConstructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(generatedConstructor));
                publicConstructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(publicConstructor);
            }

            // The instance can be a reverse proxy type of this registry, whose ToString dynamic duck typing inspects.
            var targetToString = (importedTargetType is TypeDef generatedTargetType ? FindPublicToString(generatedTargetType) : null) ?? FindPublicToString(targetType);
            EmitIDuckTypeImplementation(moduleDef, generatedType, importedTargetType, targetField, importedMembers, targetIsValueType, targetToString, targetType, closedGenericTargetTypeArguments, reportedTargetTypeField);
            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyType);
            var generatedInterfaceProperties = new Dictionary<string, PropertyDef>(StringComparer.Ordinal);
            var generatedInterfaceImplementations = new HashSet<string>(StringComparer.Ordinal)
            {
                BuildTypeDefOrRefIdentityKey(proxyType)
            };

            foreach (var binding in bindings!)
            {
                var proxyMethod = binding.ProxyMethod;
                var interfaceMethodContract = isInterfaceProxy ? proxyTypePlan?.GetInterfaceMethodContract(proxyMethod) : null;

                // Like the methods dynamic duck typing defines, the methods of a proxy have no custom modifiers (e.g. the modreq of
                // an init accessor or an `in` parameter): they override (or implement) the methods with the same name and signature,
                // so a member with custom modifiers isn't implemented.
                var generatedMethodSig = CreateGeneratedProxyMethodSig(moduleDef, proxyMethod, GetProxyMethodTypeArguments(proxyType, targetType, proxyMethod, isInterfaceProxy, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments), interfaceMethodContract);
                var generatedMethod = new MethodDefUser(
                    proxyMethod.Name,
                    WithoutCustomModifiers(generatedMethodSig),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    isInterfaceProxy ? GetInterfaceMethodAttributes(proxyMethod) : GetClassOverrideMethodAttributes(proxyMethod, isReverseMapping));

                CopyMethodGenericParameters(moduleDef, proxyMethod, generatedMethod);
                if (!proxyMethod.IsSpecialName)
                {
                    CopyProxyMethodParameters(proxyMethod, generatedMethod);
                }

                AddInterfaceMethodOverride(moduleDef, generatedType, generatedInterfaceImplementations, isInterfaceProxy, proxyType, generatedMethod, proxyMethod, interfaceMethodContract, closedGenericProxyTypeArguments);
                generatedMethod.Body = new CilBody();
                switch (binding.Kind)
                {
                    case ForwardBindingKind.Method:
                    {
                        var branchStopwatch = StartProfilePhase();
                        try
                        {
                            var targetMethod = binding.TargetMethod!;
                            var methodBinding = binding.MethodBinding!.Value;
                            var byRefWriteBacks = new List<ByRefWriteBackPlan>();
                            if (!targetMethod.IsStatic)
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                                generatedMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                            }

                            for (var parameterIndex = 0; parameterIndex < proxyMethod.MethodSig.Params.Count; parameterIndex++)
                            {
                                var parameterBinding = methodBinding.ParameterBindings[parameterIndex];
                                var proxyParameter = generatedMethod.Parameters[parameterIndex + 1];
                                if (parameterBinding.IsByRef && parameterBinding.UseLocalForByRef)
                                {
                                    var targetElementTypeSig = ImportTypeSigCached(moduleDef, parameterBinding.TargetByRefElementTypeSig!, $"target by-ref element type for '{targetMethod.FullName}'");
                                    var targetByRefLocal = new Local(targetElementTypeSig);
                                    generatedMethod.Body.Variables.Add(targetByRefLocal);
                                    generatedMethod.Body.InitLocals = true;

                                    if (!parameterBinding.IsOut)
                                    {
                                        generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, proxyParameter));
                                        EmitLoadByRefValue(moduleDef, generatedMethod.Body, parameterBinding.ProxyByRefElementTypeSig!, $"proxy parameter '{proxyMethod.FullName}'");
                                        EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, parameterBinding.PreCallConversion, importedMembers, $"target parameter of method '{targetMethod.FullName}'", preserveNullForDuckTypeExtraction: true);
                                        generatedMethod.Body.Instructions.Add(OpCodes.Stloc.ToInstruction(targetByRefLocal));
                                    }

                                    generatedMethod.Body.Instructions.Add(OpCodes.Ldloca.ToInstruction(targetByRefLocal));
                                    byRefWriteBacks.Add(new ByRefWriteBackPlan(proxyParameter, targetByRefLocal, parameterBinding));
                                }
                                else
                                {
                                    generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, proxyParameter));
                                    if (!parameterBinding.IsByRef)
                                    {
                                        EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, parameterBinding.PreCallConversion, importedMembers, $"target parameter of method '{targetMethod.FullName}'", preserveNullForDuckTypeExtraction: false);
                                    }
                                }
                            }

                            EmitTrailingOptionalTargetArguments(
                                moduleDef,
                                generatedMethod.Body,
                                targetMethod,
                                methodBinding,
                                GetClassMemberGenericTypeArguments(targetType, targetMethod.DeclaringType, closedGenericTargetTypeArguments));

                            var calledTargetMethod = GetTargetCallOpCode(moduleDef, targetType, targetMethod, targetIsValueType) == OpCodes.Callvirt
                                                         ? GetPortableVirtualCallTarget(targetType, targetMethod)
                                                         : targetMethod;
                            var importedTargetMethod = ImportMethodDefOrRefCached(moduleDef, calledTargetMethod, $"target method '{calledTargetMethod.FullName}'");
                            var targetMethodToCall = CreateMethodCallTarget(
                                moduleDef,
                                importedTargetMethod,
                                ImportMemberDeclaringType(moduleDef, targetType, calledTargetMethod.DeclaringType, closedGenericTargetTypeArguments),
                                generatedMethod,
                                methodBinding.ClosedGenericMethodArguments);
                            if (!targetMethod.IsStatic && targetIsValueType && (targetMethod.IsVirtual || targetMethod.DeclaringType.IsInterface))
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                                generatedMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(targetMethodToCall));
                            }
                            else
                            {
                                generatedMethod.Body.Instructions.Add(GetTargetCallOpCode(moduleDef, targetType, targetMethod, targetIsValueType).ToInstruction(targetMethodToCall));
                            }

                            foreach (var byRefWriteBack in byRefWriteBacks)
                            {
                                generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, byRefWriteBack.ProxyParameter));
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldloc.ToInstruction(byRefWriteBack.TargetLocal));
                                EmitMethodReturnConversion(moduleDef, generatedMethod.Body, byRefWriteBack.ParameterBinding.PostCallConversion, importedMembers, $"proxy parameter '{proxyMethod.FullName}'");
                                EmitStoreByRefValue(moduleDef, generatedMethod.Body, byRefWriteBack.ParameterBinding.ProxyByRefElementTypeSig!, $"proxy parameter '{proxyMethod.FullName}'");
                            }

                            EmitMethodReturnConversion(moduleDef, generatedMethod.Body, methodBinding.ReturnConversion, importedMembers, $"target method '{targetMethod.FullName}'");
                        }
                        finally
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.EmitForwardMethodBodyCount++;
                            }

                            StopProfilePhase(branchStopwatch, seconds => _currentProfile!.EmitForwardMethodBodySeconds += seconds);
                        }

                        break;
                    }

                    case ForwardBindingKind.FieldGet:
                    {
                        var branchStopwatch = StartProfilePhase();
                        try
                        {
                            var fieldBinding = binding.FieldBinding!.Value;
                            var importedTargetMemberField = ImportTargetField(moduleDef, binding.TargetField!, targetType, closedGenericTargetTypeArguments);
                            if (binding.TargetField!.IsStatic)
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(importedTargetMemberField));
                            }
                            else
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                                generatedMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(importedTargetMemberField));
                            }

                            EmitMethodReturnConversion(moduleDef, generatedMethod.Body, fieldBinding.ReturnConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'");
                        }
                        finally
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.EmitForwardFieldGetBodyCount++;
                            }

                            StopProfilePhase(branchStopwatch, seconds => _currentProfile!.EmitForwardFieldGetBodySeconds += seconds);
                        }

                        break;
                    }

                    case ForwardBindingKind.FieldSet:
                    {
                        var branchStopwatch = StartProfilePhase();
                        try
                        {
                            var fieldBinding = binding.FieldBinding!.Value;
                            var importedTargetMemberField = ImportTargetField(moduleDef, binding.TargetField!, targetType, closedGenericTargetTypeArguments);
                            if (binding.TargetField!.IsStatic)
                            {
                                generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                                EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'", preserveNullForDuckTypeExtraction: false);
                                generatedMethod.Body.Instructions.Add(OpCodes.Stsfld.ToInstruction(importedTargetMemberField));
                            }
                            else
                            {
                                generatedMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                                generatedMethod.Body.Instructions.Add((targetIsValueType ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(targetField));
                                generatedMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg, generatedMethod.Parameters[1]));
                                EmitMethodArgumentConversion(moduleDef, generatedMethod.Body, fieldBinding.ArgumentConversion, importedMembers, $"target field '{binding.TargetField!.FullName}'", preserveNullForDuckTypeExtraction: false);
                                generatedMethod.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(importedTargetMemberField));
                            }
                        }
                        finally
                        {
                            if (_currentProfile is not null)
                            {
                                _currentProfile.EmitForwardFieldSetBodyCount++;
                            }

                            StopProfilePhase(branchStopwatch, seconds => _currentProfile!.EmitForwardFieldSetBodySeconds += seconds);
                        }

                        break;
                    }

                    default:
                        throw new InvalidOperationException($"Unsupported forward binding kind '{binding.Kind}'.");
                }

                generatedMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(generatedMethod);

                // Like dynamic duck typing, a reverse proxy's property is named after the delegation's property, with the type of the
                // property it implements.
                var implementationProperty = isReverseMapping && binding.Kind == ForwardBindingKind.Method && binding.TargetMethod is { } implementationAccessor
                                                 ? FindPropertyFromAccessor(implementationAccessor)
                                                 : null;
                EnsureInterfacePropertyMetadata(moduleDef, generatedType, proxyMethod, generatedMethod, closedGenericProxyTypeArguments, generatedInterfaceProperties, implementationProperty);
            }

            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(resolvedProxyContractTypeSig, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            activatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            if (reportedTargetTypeField is not null)
            {
                activatorMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            }

            activatorMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(generatedConstructor));
            // Returning a struct proxy through an interface contract requires boxing at this boundary.
            if (emitInterfaceStructProxy)
            {
                activatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(generatedType));
            }

            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            var registrationActivatorMethod = new MethodDefUser(
                $"ActivateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            // Bootstrap registration stays object-based while preserving a typed activator method for normal execution paths.
            EmitActivatorInstanceCast(registrationActivatorMethod.Body, importedMembers, importedTargetType, targetIsValueType);
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            if (proxyType.IsValueType)
            {
                registrationActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(ImportTypeDefOrRefCached(moduleDef, proxyType, $"proxy type '{proxyType.FullName}'")));
            }

            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(resolvedProxyContractType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            // Registration must preserve the generated implementation type because runtime duplicate detection uses it.
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(generatedType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            EmitTypedActivatorDelegate(moduleDef, bootstrapType, initializeMethod.Body, importedMembers, mappingIndex, resolvedProxyContractTypeSig, importedTargetType, targetIsValueType, activatorMethod, registrationActivatorMethod);
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(isReverseMapping ? importedMembers.RegisterTypedReverseProxyMethod : importedMembers.RegisterTypedProxyMethod));

            if (reportedTargetTypeField is not null)
            {
                var fallbackActivatorMethod = new MethodDefUser(
                    $"ActivateFallbackProxy_{mappingIndex:D4}",
                    MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object, importedMembers.SystemTypeSig),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
                fallbackActivatorMethod.Body = new CilBody();
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Castclass.ToInstruction(isArrayTarget ? systemArrayType : importedTargetType));
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(generatedConstructor));
                if (emitInterfaceStructProxy)
                {
                    fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(generatedType));
                }

                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                bootstrapType.Methods.Add(fallbackActivatorMethod);
                EmitFallbackRegistration(initializeMethod.Body, importedMembers, resolvedProxyContractType, importedTargetType, generatedType, fallbackActivatorMethod, servesDerivedTypes);
            }

            _currentExecutionContext?.GeneratedProxyTypes.Add(new KeyValuePair<string, string>(registrationKey, generatedType.ReflectionFullName));
            return DuckTypeAotMappingEmissionResult.Compatible(
                mapping,
                moduleDef.Assembly?.Name?.String ?? string.Empty,
                generatedType.FullName);
        }

        /// <summary>
        /// Emits closed generic mapping.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="isReverseMapping">The is reverse mapping value.</param>
        /// <param name="proxyModulesByAssemblyName">The proxy modules by assembly name value.</param>
        /// <param name="targetModulesByAssemblyName">The target modules by assembly name value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="emissionWarnings">The emission warnings value.</param>
        /// <param name="generatedReverseTarget">The generated reverse proxy type the proxy is created for, instead of the target.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotMappingEmissionResult EmitClosedGenericMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            bool isReverseMapping,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, ModuleDefMD> targetModulesByAssemblyName,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string> emissionWarnings,
            TypeDef? generatedReverseTarget = null)
        {
            if (!proxyAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy assembly '{mapping.ProxyAssemblyName}' was not loaded.");
            }

            if (!targetAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target assembly '{mapping.TargetAssemblyName}' was not loaded.");
            }

            if (!TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) || proxyRuntimeType is null)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy closed generic type '{mapping.ProxyTypeName}' was not found in '{mapping.ProxyAssemblyName}'.");
            }

            if (!TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType) || targetRuntimeType is null)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target closed generic type '{mapping.TargetTypeName}' was not found in '{mapping.TargetAssemblyName}'.");
            }

            // Like dynamic duck typing, a proxy is created even when the target already implements the proxy contract
            // (only the generic CreateCache<T>.Create short-circuits that case, in both engines): it honors the [Duck]
            // renames and exposes IDuckType.
            if (!proxyModulesByAssemblyName.TryGetValue(mapping.ProxyAssemblyName, out var proxyModule))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy assembly '{mapping.ProxyAssemblyName}' was not loaded.");
            }

            if (!targetModulesByAssemblyName.TryGetValue(mapping.TargetAssemblyName, out var targetModule))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target assembly '{mapping.TargetAssemblyName}' was not loaded.");
            }

            var proxyTypeDefinitionName = proxyRuntimeType.IsGenericType
                                              ? proxyRuntimeType.GetGenericTypeDefinition().FullName!
                                              : mapping.ProxyTypeName;
            if (!TryResolveType(proxyModule, proxyTypeDefinitionName, out var proxyType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy type definition '{proxyTypeDefinitionName}' was not found in '{mapping.ProxyAssemblyName}'.");
            }

            var closedTargetRuntimeType = targetRuntimeType!;
            var targetRuntimeTypeDefinition = closedTargetRuntimeType.IsGenericType
                                                  ? closedTargetRuntimeType.GetGenericTypeDefinition()
                                                  : closedTargetRuntimeType;
            if (!TryResolveType(targetModule, targetRuntimeTypeDefinition.FullName!, out var targetType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target type definition '{targetRuntimeTypeDefinition.FullName}' was not found in '{mapping.TargetAssemblyName}'.");
            }

            var closedGenericTargetTypeArguments = closedTargetRuntimeType.IsGenericType
                                                       ? closedTargetRuntimeType.GetGenericArguments()
                                                                              .Select(runtimeType => ImportRuntimeTypeSig(moduleDef, runtimeType))
                                                                              .ToArray()
                                                       : null;
            var closedGenericProxyTypeArguments = proxyRuntimeType.IsGenericType
                                                      ? proxyRuntimeType.GetGenericArguments()
                                                                        .Select(runtimeType => ImportRuntimeTypeSig(moduleDef, runtimeType))
                                                                        .ToArray()
                                                      : null;

            // A generated reverse proxy type deriving from (or implementing) the mapped target is the instance of the proxy instead.
            var importedClosedTargetType = generatedReverseTarget ?? ImportRuntimeTypeCached(moduleDef, closedTargetRuntimeType, $"closed generic target type '{mapping.TargetTypeName}'");
            var importedClosedTargetTypeSig = generatedReverseTarget?.ToTypeSig() ?? ImportRuntimeTypeSig(moduleDef, closedTargetRuntimeType);
            var importedClosedProxyType = proxyRuntimeType.IsGenericType
                                              ? ImportRuntimeTypeCached(moduleDef, proxyRuntimeType, $"closed generic proxy type '{mapping.ProxyTypeName}'")
                                              : null;
            var importedClosedProxyTypeSig = proxyRuntimeType.IsGenericType
                                                 ? ImportRuntimeTypeSig(moduleDef, proxyRuntimeType)
                                                 : null;

            return EmitResolvedTypeMapping(
                moduleDef,
                bootstrapType,
                initializeMethod,
                importedMembers,
                mapping,
                mappingIndex,
                isReverseMapping,
                proxyType,
                targetType,
                importedClosedTargetType,
                importedClosedTargetTypeSig,
                generatedReverseTarget?.IsValueType ?? closedTargetRuntimeType.IsValueType,
                closedGenericTargetTypeArguments,
                importedClosedProxyType,
                importedClosedProxyTypeSig,
                closedGenericProxyTypeArguments,
                proxyAssemblyPathsByName,
                targetAssemblyPathsByName,
                emissionWarnings);
        }

        /// <summary>
        /// Determines whether the generator can load the proxy and target types of a mapping, so the runtime can tell whether the
        /// proxy type it generates loads (see ValidateGeneratedRegistry).
        /// </summary>
        /// <param name="mapping">The mapping.</param>
        /// <returns>true if both types load in the generator; otherwise, false.</returns>
        private static bool CanLoadMappingTypes(DuckTypeAotMapping mapping)
            => runtimeTypeResolutionAssemblyPathsByName is { } assemblyPaths &&
               assemblyPaths.TryGetValue(DuckTypeAotNameHelpers.NormalizeAssemblyName(mapping.ProxyAssemblyName), out var proxyAssemblyPath) &&
               assemblyPaths.TryGetValue(DuckTypeAotNameHelpers.NormalizeAssemblyName(mapping.TargetAssemblyName), out var targetAssemblyPath) &&
               TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) &&
               TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath, mapping.TargetTypeName, out var targetRuntimeType) &&
               proxyRuntimeType is not null &&
               targetRuntimeType is not null;

        /// <summary>
        /// Detects, from metadata, proxy types that can't be loaded: a sealed base type, a static abstract interface member, or
        /// an abstract member of a forward proxy ignored with [DuckIgnore] that nothing implements (reverse proxies implement
        /// those too). Reflection.Emit fails to create such a type in dynamic duck typing, and a generated one would make the
        /// whole registry fail to load. Only used when the generator can't ask dynamic duck typing itself (types it can't load,
        /// contracts with their own duck attributes).
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="proxyType">The proxy type value (the base type of reverse proxies).</param>
        /// <param name="isInterfaceProxy">Whether the proxy type is an interface.</param>
        /// <param name="failure">The failure to register, with the dynamic duck typing message.</param>
        /// <param name="reason">Why the type can't be created.</param>
        /// <returns>true if the proxy type can't be created; otherwise, false.</returns>
        private static bool TryGetUnloadableProxyTypeFailure(
            DuckTypeAotMapping mapping,
            TypeDef proxyType,
            bool isInterfaceProxy,
            out DuckTypeAotMappingEmissionResult? failure,
            out string? reason)
        {
            failure = null;
            reason = null;
            var hierarchy = GetProxyTypeHierarchy(proxyType, isInterfaceProxy);
            if (!isInterfaceProxy && proxyType.IsSealed)
            {
                reason = $"its base type '{proxyType.FullName}' is sealed";
            }
            else if (isInterfaceProxy && hierarchy.Keys.SelectMany(type => type.Methods).FirstOrDefault(method => method.IsStatic && method.IsAbstract) is { } staticAbstractMethod)
            {
                reason = $"the static abstract member '{staticAbstractMethod.Name}' has no implementation";
            }
            else if (mapping.Mode == DuckTypeAotMappingMode.Forward && TryGetUnimplementedIgnoredMember(hierarchy, isInterfaceProxy, out var ignoredMemberName))
            {
                reason = $"the abstract member '{ignoredMemberName}' is ignored with [DuckIgnore], so it has no implementation";
            }
            else if (mapping.Mode == DuckTypeAotMappingMode.Forward &&
                     _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyType).ImplementedAccessors is { } implementedAccessors &&
                     TryGetUnimplementedAbstractAccessor(proxyType, hierarchy, implementedAccessors, isInterfaceProxy, out var unimplementedAccessor))
            {
                reason = $"the abstract accessor '{unimplementedAccessor!.Name}' isn't one of a property dynamic duck typing implements, so it has no implementation";
            }
            else if (mapping.Mode == DuckTypeAotMappingMode.Forward &&
                     !isInterfaceProxy &&
                     _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyType).ImplementedAccessors.FirstOrDefault(accessor => IsFinalMethodDynamicDuckTypingOverrides(proxyType, accessor)) is { } finalAccessor)
            {
                // E.g. the implementation of an interface member, or a sealed override: the override dynamic duck typing defines
                // for it can't be created.
                reason = $"the accessor '{finalAccessor.Name}' of a property dynamic duck typing implements is final, so it can't be overridden";
            }
            else
            {
                return false;
            }

            // The message of the DuckTypeException dynamic duck typing throws.
            failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                mapping,
                DuckTypeAotCompatibilityStatuses.UnsupportedProxyKind,
                StatusCodeUnloadableProxyType,
                CreateDuckTypeCreationFailureMessage(mapping.TargetTypeName, mapping.ProxyTypeName));
            return true;
        }

        /// <summary>
        /// Determines whether the method dynamic duck typing defines for an implemented accessor overrides a final method, which
        /// fails: it has the name and the signature of the accessor, without custom modifiers, and is virtual without NewSlot, so
        /// it overrides the most derived virtual method with that name and exact signature (an accessor with custom modifiers,
        /// e.g. an init setter, has another signature).
        /// </summary>
        /// <param name="proxyType">The class proxy type.</param>
        /// <param name="accessor">The implemented accessor.</param>
        /// <returns>true if the overridden method is final; otherwise, false.</returns>
        private static bool IsFinalMethodDynamicDuckTypingOverrides(TypeDef proxyType, MethodDef accessor)
        {
            if (HasCustomModifiers(accessor.MethodSig))
            {
                return false;
            }

            var accessorArguments = accessor.DeclaringType is { IsInterface: false } accessorDeclaringType ? GetClassMemberGenericTypeArguments(proxyType, accessorDeclaringType, closedRootTypeArguments: null) : null;
            for (var current = proxyType; current is not null; current = current.BaseType?.ResolveTypeDef())
            {
                foreach (var method in current.Methods)
                {
                    if (method.IsVirtual &&
                        !method.IsStatic &&
                        string.Equals(method.Name, accessor.Name, StringComparison.Ordinal) &&
                        !HasCustomModifiers(method.MethodSig) &&
                        AreEffectiveMethodSignaturesEquivalent(method.MethodSig, GetClassMemberGenericTypeArguments(proxyType, current, closedRootTypeArguments: null), accessor.MethodSig, accessorArguments))
                    {
                        return method.IsFinal;
                    }
                }
            }

            return false;

            static bool HasCustomModifiers(MethodSig signature)
                => new[] { signature.RetType }.Concat(signature.Params).Any(ContainsModifier);

            static bool ContainsModifier(TypeSig? type)
                => type switch
                {
                    ModifierSig => true,
                    GenericInstSig genericInstSig => genericInstSig.GenericArguments.Any(ContainsModifier),
                    NonLeafSig nonLeafSig => ContainsModifier(nonLeafSig.Next),
                    _ => false,
                };
        }

        /// <summary>
        /// Finds an abstract accessor of a forward proxy type nothing implements: dynamic duck typing implements the accessors of
        /// the properties it selects (see ProxyTypePlan.BuildImplementedAccessors). In a class hierarchy, the most derived
        /// declaration of a vtable slot (an override, an explicit override, a re-abstraction) decides whether it has an
        /// implementation. In an interface hierarchy, an accessor is also implemented by a default implementation of a derived
        /// interface, or by an implemented accessor with the same name and signature (the runtime binds interface members by
        /// name and signature to the public methods of the proxy type).
        /// </summary>
        /// <param name="proxyType">The proxy type.</param>
        /// <param name="hierarchy">The types the proxy type is made of, with their generic arguments.</param>
        /// <param name="implementedAccessors">The accessors dynamic duck typing implements.</param>
        /// <param name="isInterfaceProxy">Whether the proxy type is an interface.</param>
        /// <param name="unimplementedAccessor">The abstract accessor nothing implements.</param>
        /// <returns>true if an abstract accessor has no implementation; otherwise, false.</returns>
        private static bool TryGetUnimplementedAbstractAccessor(
            TypeDef proxyType,
            IReadOnlyDictionary<TypeDef, IReadOnlyList<TypeSig>?> hierarchy,
            IReadOnlyCollection<MethodDef> implementedAccessors,
            bool isInterfaceProxy,
            out MethodDef? unimplementedAccessor)
        {
            unimplementedAccessor = null;
            if (isInterfaceProxy)
            {
                var implemented = new HashSet<MethodDef>(implementedAccessors, ReferenceIdentityComparer<MethodDef>.Instance);
                foreach (var method in hierarchy.Keys.SelectMany(type => type.Methods))
                {
                    if (method.IsAbstract)
                    {
                        continue;
                    }

                    foreach (var methodOverride in method.Overrides)
                    {
                        if (methodOverride.MethodDeclaration.ResolveMethodDef() is { } implementedMethod)
                        {
                            implemented.Add(implementedMethod);
                        }
                    }
                }

                foreach (var accessor in hierarchy.Keys.SelectMany(type => type.Methods))
                {
                    if (!accessor.IsAbstract || accessor.IsStatic || !ProxyTypePlan.IsAccessor(accessor) || IsImplemented(accessor))
                    {
                        continue;
                    }

                    // A re-abstraction (an abstract explicit override) needs an implementation of the member it re-abstracts.
                    if (accessor.Overrides.Count > 0 &&
                        accessor.Overrides.All(methodOverride => methodOverride.MethodDeclaration.ResolveMethodDef() is { } reabstracted && IsImplemented(reabstracted)))
                    {
                        continue;
                    }

                    unimplementedAccessor = accessor;
                    return true;
                }

                return false;

                bool IsImplemented(MethodDef accessor)
                    => implemented.Contains(accessor) ||
                       (hierarchy.TryGetValue(accessor.DeclaringType, out var accessorArguments) &&
                        implementedAccessors.Any(
                            other => string.Equals(other.Name, accessor.Name, StringComparison.Ordinal) &&
                                     hierarchy.TryGetValue(other.DeclaringType, out var otherArguments) &&
                                     AreEffectiveMethodSignaturesEquivalent(other.MethodSig, otherArguments, accessor.MethodSig, accessorArguments)));
            }

            // The most derived declaration of each slot: the types are visited from the proxy type to its base types.
            var mostDerivedDeclarations = new Dictionary<MethodDef, MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
            for (var current = proxyType; current is not null; current = current.BaseType?.ResolveTypeDef())
            {
                foreach (var method in current.Methods)
                {
                    if (!method.IsVirtual || method.IsStatic)
                    {
                        continue;
                    }

                    foreach (var slotMethod in GetImplementedVtableSlotMethods(proxyType, method))
                    {
                        if (!mostDerivedDeclarations.ContainsKey(slotMethod))
                        {
                            mostDerivedDeclarations[slotMethod] = method;
                        }
                    }
                }
            }

            var overriddenSlots = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
            var interfaceArguments = new Dictionary<TypeDef, IReadOnlyList<TypeSig>?>();
            foreach (var interfaceEntry in ProxyTypePlan.EnumerateInterfacesWithArguments(proxyType))
            {
                if (!interfaceArguments.ContainsKey(interfaceEntry.InterfaceType))
                {
                    interfaceArguments[interfaceEntry.InterfaceType] = interfaceEntry.GenericArguments;
                }
            }

            foreach (var accessor in implementedAccessors)
            {
                if (accessor.DeclaringType?.IsInterface != true)
                {
                    overriddenSlots.Add(GetVtableSlotMethod(proxyType, accessor));
                    continue;
                }

                // The public method dynamic duck typing defines for an interface accessor overrides a base virtual method with the
                // same name and signature.
                foreach (var method in mostDerivedDeclarations.Values)
                {
                    if (string.Equals(method.Name, accessor.Name, StringComparison.Ordinal) &&
                        AreEffectiveMethodSignaturesEquivalent(
                            method.MethodSig,
                            GetClassMemberGenericTypeArguments(proxyType, method.DeclaringType, closedRootTypeArguments: null),
                            accessor.MethodSig,
                            interfaceArguments.TryGetValue(accessor.DeclaringType, out var accessorInterfaceArguments) ? accessorInterfaceArguments : null))
                    {
                        overriddenSlots.Add(GetVtableSlotMethod(proxyType, method));
                    }
                }
            }

            foreach (var slot in mostDerivedDeclarations)
            {
                if (slot.Value.IsAbstract && ProxyTypePlan.IsAccessor(slot.Value) && !overriddenSlots.Contains(slot.Key))
                {
                    unimplementedAccessor = slot.Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the types a proxy type is made of, with their generic arguments as seen from the proxy type: the inherited
        /// interfaces of an interface proxy, the base types of a class proxy.
        /// </summary>
        /// <param name="proxyType">The proxy type.</param>
        /// <param name="isInterfaceProxy">Whether the proxy type is an interface.</param>
        /// <returns>The types, with their generic arguments.</returns>
        private static Dictionary<TypeDef, IReadOnlyList<TypeSig>?> GetProxyTypeHierarchy(TypeDef proxyType, bool isInterfaceProxy)
        {
            var types = new Dictionary<TypeDef, IReadOnlyList<TypeSig>?>();
            var pending = new Stack<KeyValuePair<TypeDef, IReadOnlyList<TypeSig>?>>();
            pending.Push(new KeyValuePair<TypeDef, IReadOnlyList<TypeSig>?>(proxyType, null));
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (types.ContainsKey(current.Key))
                {
                    continue;
                }

                types.Add(current.Key, current.Value);
                var inheritedTypes = isInterfaceProxy
                                         ? current.Key.Interfaces.Select(interfaceImpl => interfaceImpl.Interface)
                                         : current.Key.BaseType is { } baseType ? new[] { baseType } : Array.Empty<ITypeDefOrRef>();
                foreach (var inheritedType in inheritedTypes)
                {
                    if (inheritedType.ResolveTypeDef() is { } inheritedTypeDef)
                    {
                        var inheritedTypeSig = SubstituteTypeAndMethodGenericTypeArguments(inheritedType.ToTypeSig(), current.Value, closedGenericMethodArguments: null);
                        pending.Push(new KeyValuePair<TypeDef, IReadOnlyList<TypeSig>?>(inheritedTypeDef, (inheritedTypeSig as GenericInstSig)?.GenericArguments.ToArray()));
                    }
                }
            }

            return types;
        }

        /// <summary>
        /// Finds an abstract member of a proxy type that is ignored with [DuckIgnore] and that nothing implements: no override
        /// in a derived proxy class, no default implementation in a derived proxy interface, and no other (not ignored) member
        /// with the same name and signature, whose implementation would also implement it.
        /// </summary>
        /// <param name="hierarchy">The types the proxy type is made of, with their generic arguments (see GetProxyTypeHierarchy).</param>
        /// <param name="isInterfaceProxy">Whether the proxy type is an interface.</param>
        /// <param name="memberName">The name of the member.</param>
        /// <returns>true if such a member exists; otherwise, false.</returns>
        private static bool TryGetUnimplementedIgnoredMember(IReadOnlyDictionary<TypeDef, IReadOnlyList<TypeSig>?> hierarchy, bool isInterfaceProxy, out string? memberName)
        {
            memberName = null;
            var methods = hierarchy.Keys.SelectMany(type => type.Methods).ToList();
            // Default implementations of a derived proxy interface (and explicit overrides) implement the members they override.
            var implementedMethods = new HashSet<MethodDef>(methods.SelectMany(method => method.Overrides)
                                                                   .Select(methodOverride => methodOverride.MethodDeclaration.ResolveMethodDef())
                                                                   .OfType<MethodDef>());
            foreach (var method in methods)
            {
                if (!method.IsAbstract || method.IsStatic || !IsDuckIgnoreMethod(method) || implementedMethods.Contains(method))
                {
                    continue;
                }

                var implemented = methods.Any(
                    other => other != method &&
                             !other.IsStatic &&
                             string.Equals(other.Name, method.Name, StringComparison.Ordinal) &&
                             AreEffectiveMethodSignaturesEquivalent(other.MethodSig, hierarchy[other.DeclaringType], method.MethodSig, hierarchy[method.DeclaringType]) &&
                             (isInterfaceProxy ? !IsDuckIgnoreMethod(other) : !other.IsAbstract && other.IsVirtual && !other.IsNewSlot));
                if (!implemented)
                {
                    memberName = method.Name;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Emits a forward mapping whose target is an array type.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="proxyModulesByAssemblyName">The proxy modules by assembly name value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <param name="emissionWarnings">The emission warnings value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotMappingEmissionResult EmitArrayTargetMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            IReadOnlyDictionary<string, ModuleDefMD> proxyModulesByAssemblyName,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ICollection<string> emissionWarnings)
        {
            if (!proxyModulesByAssemblyName.TryGetValue(mapping.ProxyAssemblyName, out var proxyModule) ||
                !proxyAssemblyPathsByName.TryGetValue(mapping.ProxyAssemblyName, out var proxyAssemblyPath))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy assembly '{mapping.ProxyAssemblyName}' was not loaded.");
            }

            if ((!targetAssemblyPathsByName.TryGetValue(mapping.TargetAssemblyName, out var targetAssemblyPath) &&
                 runtimeTypeResolutionAssemblyPathsByName?.TryGetValue(mapping.TargetAssemblyName, out targetAssemblyPath) != true) ||
                !TryResolveRuntimeType(mapping.TargetAssemblyName, targetAssemblyPath!, mapping.TargetTypeName, out var arrayRuntimeType) ||
                arrayRuntimeType is null ||
                !arrayRuntimeType.IsArray)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"Target array type '{mapping.TargetTypeName}' was not found in '{mapping.TargetAssemblyName}'.");
            }

            var proxyTypeDefinitionName = mapping.ProxyTypeName;
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments = null;
            ITypeDefOrRef? importedClosedProxyType = null;
            TypeSig? importedClosedProxyTypeSig = null;
            if (DuckTypeAotNameHelpers.IsClosedGenericTypeName(mapping.ProxyTypeName))
            {
                if (!TryResolveRuntimeType(mapping.ProxyAssemblyName, proxyAssemblyPath, mapping.ProxyTypeName, out var proxyRuntimeType) || proxyRuntimeType is null)
                {
                    return DuckTypeAotMappingEmissionResult.NotCompatible(
                        mapping,
                        DuckTypeAotCompatibilityStatuses.MissingProxyType,
                        StatusCodeMissingProxyType,
                        $"Proxy closed generic type '{mapping.ProxyTypeName}' was not found in '{mapping.ProxyAssemblyName}'.");
                }

                proxyTypeDefinitionName = proxyRuntimeType.GetGenericTypeDefinition().FullName!;
                closedGenericProxyTypeArguments = proxyRuntimeType.GetGenericArguments().Select(runtimeType => ImportRuntimeTypeSig(moduleDef, runtimeType)).ToArray();
                importedClosedProxyType = ImportRuntimeTypeCached(moduleDef, proxyRuntimeType, $"closed generic proxy type '{mapping.ProxyTypeName}'");
                importedClosedProxyTypeSig = ImportRuntimeTypeSig(moduleDef, proxyRuntimeType);
            }

            if (!TryResolveType(proxyModule, proxyTypeDefinitionName, out var proxyType))
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingProxyType,
                    StatusCodeMissingProxyType,
                    $"Proxy type '{proxyTypeDefinitionName}' was not found in '{mapping.ProxyAssemblyName}'.");
            }

            // Reflection on an array type finds the members of System.Array: bind against that definition.
            if (proxyModule.CorLibTypes.GetTypeRef("System", "Array").ResolveTypeDef() is not { } arrayTypeDefinition)
            {
                return DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    DuckTypeAotCompatibilityStatuses.MissingTargetType,
                    StatusCodeMissingTargetType,
                    $"The System.Array definition needed by target array type '{mapping.TargetTypeName}' could not be resolved.");
            }

            return EmitResolvedTypeMapping(
                moduleDef,
                bootstrapType,
                initializeMethod,
                importedMembers,
                mapping,
                mappingIndex,
                isReverseMapping: mapping.Mode == DuckTypeAotMappingMode.Reverse,
                proxyType,
                arrayTypeDefinition,
                ImportRuntimeTypeCached(moduleDef, arrayRuntimeType, $"array target type '{mapping.TargetTypeName}'"),
                ImportRuntimeTypeSig(moduleDef, arrayRuntimeType),
                targetIsValueType: false,
                closedGenericTargetTypeArguments: null,
                importedClosedProxyType,
                importedClosedProxyTypeSig,
                closedGenericProxyTypeArguments,
                proxyAssemblyPathsByName,
                targetAssemblyPathsByName,
                emissionWarnings);
        }

        /// <summary>
        /// Emits the registration of a proxy's activator for the runtime types the registry can't name (see
        /// DuckTypeAotEngine.TryGetFallbackResult).
        /// </summary>
        /// <param name="body">The registration method body.</param>
        /// <param name="importedMembers">The imported members.</param>
        /// <param name="proxyContractType">The proxy definition type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="generatedType">The generated proxy type.</param>
        /// <param name="fallbackActivatorMethod">The activator receiving the instance and the runtime type.</param>
        /// <param name="derivedTypes">Whether the registration serves the classes deriving from the target type
        /// (<c>DuckType.RegisterAotDerivedTypesProxy</c>).</param>
        private static void EmitFallbackRegistration(CilBody body, ImportedMembers importedMembers, ITypeDefOrRef proxyContractType, ITypeDefOrRef targetType, ITypeDefOrRef generatedType, MethodDef fallbackActivatorMethod, bool derivedTypes = false)
        {
            body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(proxyContractType));
            body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(targetType));
            body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(generatedType));
            body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            body.Instructions.Add(OpCodes.Ldftn.ToInstruction(fallbackActivatorMethod));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(importedMembers.FuncObjectTypeObjectCtor));
            body.Instructions.Add(OpCodes.Call.ToInstruction(derivedTypes ? importedMembers.RegisterAotDerivedTypesProxyMethod : importedMembers.RegisterAotFallbackProxyMethod));
        }

        /// <summary>
        /// Emits struct copy mapping.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="bootstrapType">The bootstrap type value.</param>
        /// <param name="initializeMethod">The initialize method value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="mappingIndex">The mapping index value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyAssemblyPathsByName">The proxy assembly paths by name value.</param>
        /// <param name="targetAssemblyPathsByName">The target assembly paths by name value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static DuckTypeAotMappingEmissionResult EmitStructCopyMapping(
            ModuleDef moduleDef,
            TypeDef bootstrapType,
            MethodDef initializeMethod,
            ImportedMembers importedMembers,
            DuckTypeAotMapping mapping,
            int mappingIndex,
            TypeDef proxyType,
            TypeDef targetType,
            IReadOnlyDictionary<string, string> proxyAssemblyPathsByName,
            IReadOnlyDictionary<string, string> targetAssemblyPathsByName,
            ITypeDefOrRef? importedTargetTypeOverride = null,
            TypeSig? importedTargetTypeSigOverride = null,
            bool? targetIsValueTypeOverride = null,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments = null,
            ITypeDefOrRef? importedProxyTypeOverride = null,
            TypeSig? importedProxyTypeSigOverride = null)
        {
            // The fields of a closed generic [DuckCopy] struct have the types of its generic arguments, and are referenced on the
            // closed type, not on the generic definition.
            var closedGenericProxyTypeArguments = (importedProxyTypeSigOverride as GenericInstSig)?.GenericArguments.ToArray();
            var planningStopwatch = StartProfilePhase();
            if (!TryCollectStructCopyBindings(mapping, proxyType, targetType, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, out var bindings, out var failure))
            {
                StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);
                return failure!;
            }

            StopProfilePhase(planningStopwatch, seconds => _currentProfile!.RegistrationPlanningSeconds += seconds);
            RecordRuntimeSpecificBindings(mapping, targetType, bindings.Select(binding => (IMemberDef?)binding.SourceProperty?.GetMethod ?? binding.SourceField));

            var dynamicValidationFailure = ValidateMappingWithDynamicEngine(moduleDef, mapping, proxyType, targetType, proxyAssemblyPathsByName, targetAssemblyPathsByName, out _);
            if (dynamicValidationFailure is not null)
            {
                return dynamicValidationFailure;
            }

            var importedTargetType = importedTargetTypeOverride ?? ImportTypeDefOrRefCached(moduleDef, targetType, $"resolved mapping target type '{targetType.FullName}'");
            // A closed generic [DuckCopy] struct is registered with its closed type, not the generic type definition.
            var importedProxyType = importedProxyTypeOverride ?? ImportTypeDefOrRefCached(moduleDef, proxyType, $"resolved mapping proxy type '{proxyType.FullName}'");

            var importedTargetTypeSig = importedTargetTypeSigOverride ?? ImportTypeSigCached(moduleDef, targetType.ToTypeSig(), $"resolved mapping target signature '{targetType.FullName}'");
            var importedProxyTypeSig = importedProxyTypeSigOverride ?? ImportTypeSigCached(moduleDef, proxyType.ToTypeSig(), $"resolved mapping proxy signature '{proxyType.FullName}'");
            var targetIsValueType = targetIsValueTypeOverride ?? targetType.IsValueType;

            // Like a proxy, the copy of an array type also serves the other array types (it reads members of System.Array, from
            // the instance as one), and the copy of a type of the core library its non-public types; a
            // [DuckCopy(IncludeDerivedTypes = true)] declaration, the classes deriving from its target type.
            var isArrayTarget = IsArrayTargetMapping(mapping);
            var servesDerivedTypes = !isArrayTarget && mapping.Mode == DuckTypeAotMappingMode.Forward && mapping.IncludesDerivedTypes && !targetType.IsValueType && !targetType.IsSealed;
            var servesFallbackTypes = isArrayTarget ? !IsSystemArrayTargetMapping(mapping) : mapping.Mode == DuckTypeAotMappingMode.Forward && (HasRuntimeInternalSubtypes(targetType) || servesDerivedTypes);
            var systemArrayType = moduleDef.CorLibTypes.GetTypeRef("System", "Array");
            if (isArrayTarget)
            {
                importedTargetTypeSig = new ClassSig(systemArrayType);
            }

            var activatorMethod = new MethodDefUser(
                $"CreateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(importedProxyTypeSig, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            activatorMethod.Body = new CilBody();
            activatorMethod.Body.InitLocals = true;

            var targetLocal = new Local(importedTargetTypeSig);
            var proxyLocal = new Local(importedProxyTypeSig);
            activatorMethod.Body.Variables.Add(targetLocal);
            activatorMethod.Body.Variables.Add(proxyLocal);

            activatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            activatorMethod.Body.Instructions.Add(OpCodes.Stloc.ToInstruction(targetLocal));

            activatorMethod.Body.Instructions.Add(OpCodes.Ldloca.ToInstruction(proxyLocal));
            activatorMethod.Body.Instructions.Add(OpCodes.Initobj.ToInstruction(importedProxyType));

            foreach (var binding in bindings!)
            {
                var importedProxyField = closedGenericProxyTypeArguments is null
                                             ? ImportFieldCached(moduleDef, binding.ProxyField, $"struct copy proxy field '{binding.ProxyField.FullName}'")
                                             : ImportTargetField(moduleDef, binding.ProxyField, proxyType, closedGenericProxyTypeArguments);
                activatorMethod.Body.Instructions.Add(OpCodes.Ldloca.ToInstruction(proxyLocal));
                EmitReadBindingValue(
                    activatorMethod.Body,
                    activatorMethod,
                    binding,
                    (body, asAddress) => body.Instructions.Add((asAddress ? OpCodes.Ldloca : OpCodes.Ldloc).ToInstruction(targetLocal)));
                activatorMethod.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(importedProxyField));
            }

            activatorMethod.Body.Instructions.Add(OpCodes.Ldloc.ToInstruction(proxyLocal));
            activatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(activatorMethod);

            // Like dynamic duck typing, the proxy type of the registration (CreateTypeResult.ProxyType) is a struct over the target
            // implementing IDuckType, with a property reading each target member the copy reads; the activator copies them into
            // the [DuckCopy] struct.
            var generatedProxyStruct = new TypeDefUser(
                GeneratedProxyNamespace,
                $"DuckTypeProxy_{mappingIndex:D4}_{ComputeStableShortHash(mapping.Key)}",
                moduleDef.CorLibTypes.GetTypeRef("System", "ValueType"))
            {
                Attributes = TypeAttributes.Public | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.Serializable
            };
            generatedProxyStruct.Interfaces.Add(new InterfaceImplUser(importedMembers.IDuckTypeType));
            moduleDef.Types.Add(generatedProxyStruct);
            var instanceField = new FieldDefUser("_currentInstance", new FieldSig(importedTargetTypeSig), FieldAttributes.Private | FieldAttributes.InitOnly);
            generatedProxyStruct.Fields.Add(instanceField);
            var proxyStructConstructor = new MethodDefUser(
                ".ctor",
                MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, importedTargetTypeSig),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            proxyStructConstructor.Body = new CilBody();
            proxyStructConstructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            proxyStructConstructor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            proxyStructConstructor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(instanceField));
            proxyStructConstructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            generatedProxyStruct.Methods.Add(proxyStructConstructor);
            EmitIDuckTypeImplementation(moduleDef, generatedProxyStruct, importedTargetType, instanceField, importedMembers, targetIsValueType, FindPublicToString(targetType), targetType, closedGenericTargetTypeArguments);
            foreach (var binding in bindings!)
            {
                var propertyType = closedGenericProxyTypeArguments is null
                                       ? ImportTypeSigCached(moduleDef, binding.ProxyField.FieldType, $"struct copy proxy field type '{binding.ProxyField.FullName}'")
                                       : ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(binding.ProxyField.FieldType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"struct copy proxy field type '{binding.ProxyField.FullName}'");
                var getter = new MethodDefUser(
                    "get_" + binding.ProxyField.Name,
                    MethodSig.CreateInstance(propertyType),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual);
                getter.Body = new CilBody();
                EmitReadBindingValue(
                    getter.Body,
                    getter,
                    binding,
                    (body, asAddress) =>
                    {
                        body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                        body.Instructions.Add((asAddress ? OpCodes.Ldflda : OpCodes.Ldfld).ToInstruction(instanceField));
                    });
                getter.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedProxyStruct.Methods.Add(getter);
                generatedProxyStruct.Properties.Add(new PropertyDefUser(binding.ProxyField.Name, PropertySig.CreateInstance(propertyType)) { GetMethod = getter });
            }

            _currentExecutionContext?.GeneratedProxyTypes.Add(new KeyValuePair<string, string>(string.Concat(mapping.Key, "|", importedTargetType.FullName), generatedProxyStruct.ReflectionFullName));

            var registrationActivatorMethod = new MethodDefUser(
                $"ActivateProxy_{mappingIndex:D4}",
                MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            registrationActivatorMethod.Body = new CilBody();
            EmitActivatorInstanceCast(registrationActivatorMethod.Body, importedMembers, importedTargetType, targetIsValueType);
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(importedProxyType));
            registrationActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            bootstrapType.Methods.Add(registrationActivatorMethod);

            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedProxyType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            initializeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(generatedProxyStruct));
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
            EmitTypedActivatorDelegate(moduleDef, bootstrapType, initializeMethod.Body, importedMembers, mappingIndex, importedProxyTypeSig, importedTargetType, targetIsValueType, activatorMethod, registrationActivatorMethod);
            initializeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.RegisterTypedProxyMethod));

            if (servesFallbackTypes)
            {
                var fallbackActivatorMethod = new MethodDefUser(
                    $"ActivateFallbackProxy_{mappingIndex:D4}",
                    MethodSig.CreateStatic(moduleDef.CorLibTypes.Object, moduleDef.CorLibTypes.Object, importedMembers.SystemTypeSig),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
                fallbackActivatorMethod.Body = new CilBody();
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Castclass.ToInstruction(isArrayTarget ? systemArrayType : importedTargetType));
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(activatorMethod));
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(importedProxyType));
                fallbackActivatorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                bootstrapType.Methods.Add(fallbackActivatorMethod);
                EmitFallbackRegistration(initializeMethod.Body, importedMembers, importedProxyType, importedTargetType, generatedProxyStruct, fallbackActivatorMethod, servesDerivedTypes);
            }

            return DuckTypeAotMappingEmissionResult.Compatible(
                mapping,
                moduleDef.Assembly?.Name?.String ?? string.Empty,
                generatedProxyStruct.FullName);

            void EmitReadBindingValue(CilBody body, MethodDef callingMethod, StructCopyFieldBinding binding, Action<CilBody, bool> loadTarget)
            {
                if (binding.SourceKind == StructCopySourceKind.Property)
                {
                    var sourceProperty = binding.SourceProperty!;
                    var sourceGetter = sourceProperty.GetMethod!;
                    var calledSourceGetter = GetTargetCallOpCode(moduleDef, targetType, sourceGetter, targetIsValueType) == OpCodes.Callvirt
                                                 ? GetPortableVirtualCallTarget(targetType, sourceGetter)
                                                 : sourceGetter;
                    var importedSourceGetter = ImportMethodDefOrRefCached(moduleDef, calledSourceGetter, $"struct copy source getter '{calledSourceGetter.FullName}'");
                    var sourceGetterToCall = CreateMethodCallTarget(
                        moduleDef,
                        importedSourceGetter,
                        ImportMemberDeclaringType(moduleDef, targetType, calledSourceGetter.DeclaringType, closedGenericTargetTypeArguments),
                        callingMethod,
                        closedGenericMethodArguments: null);

                    if (!sourceGetter.IsStatic)
                    {
                        loadTarget(body, targetIsValueType);
                    }

                    if (!sourceGetter.IsStatic && targetIsValueType && (sourceGetter.IsVirtual || sourceGetter.DeclaringType.IsInterface))
                    {
                        body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                        body.Instructions.Add(OpCodes.Callvirt.ToInstruction(sourceGetterToCall));
                    }
                    else
                    {
                        body.Instructions.Add(GetTargetCallOpCode(moduleDef, targetType, sourceGetter, targetIsValueType).ToInstruction(sourceGetterToCall));
                    }
                }
                else
                {
                    var sourceField = binding.SourceField!;
                    var importedSourceField = ImportTargetField(moduleDef, sourceField, targetType, closedGenericTargetTypeArguments);
                    if (sourceField.IsStatic)
                    {
                        body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(importedSourceField));
                    }
                    else
                    {
                        loadTarget(body, targetIsValueType);
                        body.Instructions.Add(OpCodes.Ldfld.ToInstruction(importedSourceField));
                    }
                }

                EmitMethodReturnConversion(
                    moduleDef,
                    body,
                    binding.ReturnConversion,
                    importedMembers,
                    $"target member for struct field '{binding.ProxyField.FullName}'");
            }
        }

        /// <summary>
        /// Copies method generic parameters and constraints to the generated method.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="sourceMethod">The source method value.</param>
        /// <param name="targetMethod">The target method value.</param>
        private static void CopyMethodGenericParameters(ModuleDef moduleDef, MethodDef sourceMethod, MethodDef targetMethod)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                if (sourceMethod.GenericParameters.Count == 0)
                {
                    return;
                }

                foreach (var sourceGenericParameter in sourceMethod.GenericParameters)
                {
                    var copiedGenericParameter = new GenericParamUser(sourceGenericParameter.Number, sourceGenericParameter.Flags, sourceGenericParameter.Name)
                    {
                        Kind = sourceGenericParameter.Kind
                    };

                    foreach (var constraint in sourceGenericParameter.GenericParamConstraints)
                    {
                        if (constraint.Constraint is null)
                        {
                            throw new InvalidOperationException($"Unable to import generic parameter constraint '(null)' for '{sourceMethod.FullName}'.");
                        }

                        // Like dynamic duck typing, a constraint built on a generic parameter of a type isn't copied: the
                        // generated type has none (e.g. the proxy of a closed generic interface).
                        if (constraint.Constraint is TypeSpec { TypeSig: { } constraintSig } && ContainsTypeGenericParameter(constraintSig))
                        {
                            continue;
                        }

                        var importedConstraint = ImportTypeDefOrRefCached(moduleDef, constraint.Constraint, $"generic parameter constraint '{constraint.Constraint.FullName}' for '{sourceMethod.FullName}'");
                        copiedGenericParameter.GenericParamConstraints.Add(new GenericParamConstraintUser(importedConstraint));
                    }

                    targetMethod.GenericParameters.Add(copiedGenericParameter);
                }
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.CopyMethodGenericParametersSeconds += seconds);
            }

            static bool ContainsTypeGenericParameter(TypeSig? candidate)
                => candidate switch
                {
                    GenericVar => true,
                    GenericInstSig genericInstSig => genericInstSig.GenericArguments.Any(ContainsTypeGenericParameter),
                    NonLeafSig nonLeafSig => ContainsTypeGenericParameter(nonLeafSig.Next),
                    _ => false,
                };
        }

        /// <summary>
        /// Creates the generated proxy method signature, substituting closed proxy generic arguments when required.
        /// </summary>
        /// <param name="moduleDef">The destination module definition.</param>
        /// <param name="proxyMethod">The source proxy method definition.</param>
        /// <param name="closedGenericProxyTypeArguments">The closed proxy generic type arguments.</param>
        /// <param name="interfaceMethodContract">The interface contract that declared the method.</param>
        /// <returns>The emitted method signature.</returns>
        private static MethodSig CreateGeneratedProxyMethodSig(
            ModuleDef moduleDef,
            MethodDef proxyMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            InterfaceMethodContract? interfaceMethodContract)
        {
            if (interfaceMethodContract is not null &&
                interfaceMethodContract.RequiresExplicitInterfaceImplementation)
            {
                return CreateImportedInterfaceContractMethodSig(
                    moduleDef,
                    interfaceMethodContract.MethodSig,
                    closedGenericProxyTypeArguments,
                    $"inherited interface method '{proxyMethod.FullName}'");
            }

            if (closedGenericProxyTypeArguments is null || closedGenericProxyTypeArguments.Count == 0)
            {
                return ImportMethodCached(moduleDef, proxyMethod, $"proxy method '{proxyMethod.FullName}'").MethodSig;
            }

            var sourceSig = proxyMethod.MethodSig;
            var returnType = ImportTypeSigCached(
                moduleDef,
                SubstituteTypeAndMethodGenericTypeArguments(sourceSig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null),
                $"closed generic proxy method return type '{proxyMethod.FullName}'");
            var parameterTypes = new TypeSig[sourceSig.Params.Count];
            for (var parameterIndex = 0; parameterIndex < parameterTypes.Length; parameterIndex++)
            {
                parameterTypes[parameterIndex] = ImportTypeSigCached(
                    moduleDef,
                    SubstituteTypeAndMethodGenericTypeArguments(sourceSig.Params[parameterIndex], closedGenericProxyTypeArguments, closedGenericMethodArguments: null),
                    $"closed generic proxy method parameter '{proxyMethod.FullName}'");
            }

            MethodSig generatedSig;
            if (sourceSig.Generic)
            {
                generatedSig = sourceSig.HasThis
                                   ? MethodSig.CreateInstanceGeneric(sourceSig.GenParamCount, returnType, parameterTypes)
                                   : MethodSig.CreateStaticGeneric(sourceSig.GenParamCount, returnType, parameterTypes);
            }
            else
            {
                generatedSig = sourceSig.HasThis
                                   ? MethodSig.CreateInstance(returnType, parameterTypes)
                                   : MethodSig.CreateStatic(returnType, parameterTypes);
            }

            generatedSig.ExplicitThis = sourceSig.ExplicitThis;
            return generatedSig;
        }

        /// <summary>
        /// Creates the generated proxy method signature for an inherited interface contract.
        /// </summary>
        /// <param name="moduleDef">The destination module definition.</param>
        /// <param name="sourceSig">The source method signature.</param>
        /// <param name="closedGenericProxyTypeArguments">The closed proxy generic type arguments.</param>
        /// <param name="context">The operation context.</param>
        /// <returns>The emitted method signature.</returns>
        private static MethodSig CreateImportedInterfaceContractMethodSig(
            ModuleDef moduleDef,
            MethodSig sourceSig,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            string context)
        {
            var returnType = ImportInterfaceContractTypeSig(
                moduleDef,
                SubstituteTypeAndMethodGenericTypeArguments(sourceSig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null),
                $"{context} return type");
            var parameterTypes = new TypeSig[sourceSig.Params.Count];
            for (var parameterIndex = 0; parameterIndex < parameterTypes.Length; parameterIndex++)
            {
                parameterTypes[parameterIndex] = ImportInterfaceContractTypeSig(
                    moduleDef,
                    SubstituteTypeAndMethodGenericTypeArguments(sourceSig.Params[parameterIndex], closedGenericProxyTypeArguments, closedGenericMethodArguments: null),
                    $"{context} parameter type");
            }

            MethodSig generatedSig;
            if (sourceSig.Generic)
            {
                generatedSig = sourceSig.HasThis
                                   ? MethodSig.CreateInstanceGeneric(sourceSig.GenParamCount, returnType, parameterTypes)
                                   : MethodSig.CreateStaticGeneric(sourceSig.GenParamCount, returnType, parameterTypes);
            }
            else
            {
                generatedSig = sourceSig.HasThis
                                   ? MethodSig.CreateInstance(returnType, parameterTypes)
                                   : MethodSig.CreateStatic(returnType, parameterTypes);
            }

            generatedSig.ExplicitThis = sourceSig.ExplicitThis;
            return generatedSig;
        }

        /// <summary>
        /// Creates method call target.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="importedTargetMethod">The imported target method value.</param>
        /// <param name="importedTargetType">The imported target type value.</param>
        /// <param name="generatedMethod">The generated method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethod CreateMethodCallTarget(
            ModuleDef moduleDef,
            IMethodDefOrRef importedTargetMethod,
            ITypeDefOrRef importedTargetType,
            MethodDef generatedMethod,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var cacheKey = BuildMethodCallTargetCacheKey(importedTargetMethod, importedTargetType, (int)generatedMethod.MethodSig.GenParamCount, closedGenericMethodArguments);
                if (_currentExecutionContext?.TryGetMethodCallTarget(cacheKey, out var cachedCallTarget) == true)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.MethodCallTargetCacheHits++;
                    }

                    return cachedCallTarget!;
                }

                if (_currentProfile is not null)
                {
                    _currentProfile.MethodCallTargetCacheMisses++;
                }

                IMethodDefOrRef effectiveTargetMethod = importedTargetMethod;
                if (importedTargetMethod.DeclaringType is ITypeDefOrRef importedDeclaringType &&
                    !string.Equals(importedDeclaringType.FullName, importedTargetType.FullName, StringComparison.Ordinal))
                {
                    // Bind the call to the imported target type so closed-generic target methods
                    // are emitted against the instantiated declaring type instead of the generic definition.
                    var reboundMethod = new MemberRefUser(
                        moduleDef,
                        importedTargetMethod.Name,
                        importedTargetMethod.MethodSig,
                        importedTargetType);
                    effectiveTargetMethod = moduleDef.UpdateRowId(reboundMethod);
                }

                if (closedGenericMethodArguments is not null && closedGenericMethodArguments.Count > 0)
                {
                    var closedArguments = new List<TypeSig>(closedGenericMethodArguments.Count);
                    for (var i = 0; i < closedGenericMethodArguments.Count; i++)
                    {
                        closedArguments.Add(ImportTypeSigCached(moduleDef, closedGenericMethodArguments[i], $"closed generic method argument '{closedGenericMethodArguments[i].FullName}'"));
                    }

                    var methodSpecWithClosedArguments = new MethodSpecUser(effectiveTargetMethod, new GenericInstMethodSig(closedArguments));
                    var closedGenericCallTarget = moduleDef.UpdateRowId(methodSpecWithClosedArguments);
                    _currentExecutionContext?.CacheMethodCallTarget(cacheKey, closedGenericCallTarget);
                    return closedGenericCallTarget;
                }

                if (generatedMethod.MethodSig.GenParamCount == 0)
                {
                    _currentExecutionContext?.CacheMethodCallTarget(cacheKey, effectiveTargetMethod);
                    return effectiveTargetMethod;
                }

                var genericArguments = new List<TypeSig>((int)generatedMethod.MethodSig.GenParamCount);
                for (var genericParameterIndex = 0; genericParameterIndex < generatedMethod.MethodSig.GenParamCount; genericParameterIndex++)
                {
                    genericArguments.Add(new GenericMVar((uint)genericParameterIndex));
                }

                var methodSpec = new MethodSpecUser(effectiveTargetMethod, new GenericInstMethodSig(genericArguments));
                var generatedMethodCallTarget = moduleDef.UpdateRowId(methodSpec);
                _currentExecutionContext?.CacheMethodCallTarget(cacheKey, generatedMethodCallTarget);
                return generatedMethodCallTarget;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.MethodCallTargetSeconds += seconds);
            }
        }

        /// <summary>
        /// Emits method return conversion.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="methodBody">The method body value.</param>
        /// <param name="conversion">The conversion value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="context">The context value.</param>
        private static void EmitMethodReturnConversion(
            ModuleDef moduleDef,
            CilBody methodBody,
            MethodReturnConversion conversion,
            ImportedMembers importedMembers,
            string context)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                if (conversion.Kind == MethodReturnConversionKind.WrapValueWithType ||
                    conversion.Kind == MethodReturnConversionKind.WrapValueWithTypeAfterDuckChainToProxy ||
                    conversion.Kind == MethodReturnConversionKind.WrapValueWithTypeAfterTypeConversion)
                {
                    if (!TryGetValueWithTypeArgument(conversion.WrapperTypeSig!, out var wrapperValueType))
                    {
                        throw new InvalidOperationException($"Expected ValueWithType<T> wrapper for '{conversion.WrapperTypeSig!.FullName}'.");
                    }

                    if (conversion.Kind == MethodReturnConversionKind.WrapValueWithTypeAfterDuckChainToProxy)
                    {
                        EmitDuckChainToProxyConversion(
                            moduleDef,
                            methodBody,
                            wrapperValueType!,
                            conversion.InnerTypeSig!,
                            context,
                            conversion.IsReverseDuckChaining);
                    }
                    else if (conversion.Kind == MethodReturnConversionKind.WrapValueWithTypeAfterTypeConversion)
                    {
                        EmitTypeConversion(
                            moduleDef,
                            methodBody,
                            conversion.InnerTypeSig!,
                            wrapperValueType!,
                            context);
                    }

                    // ValueWithType<T>.Type stores the original target return type seen before adaptation.
                    var importedTargetReturnType = ResolveImportedTypeForTypeToken(moduleDef, conversion.InnerTypeSig!, context);
                    methodBody.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetReturnType));
                    methodBody.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));

                    var createMethodRef = CreateValueWithTypeCreateMethodRef(
                        moduleDef,
                        conversion.WrapperTypeSig!,
                        wrapperValueType!);
                    methodBody.Instructions.Add(OpCodes.Call.ToInstruction(createMethodRef));
                    return;
                }

                if (conversion.Kind == MethodReturnConversionKind.TypeConversion)
                {
                    EmitTypeConversion(moduleDef, methodBody, conversion.WrapperTypeSig!, conversion.InnerTypeSig!, context);
                    return;
                }

                if (conversion.Kind == MethodReturnConversionKind.DuckChainToProxy)
                {
                    EmitDuckChainToProxyConversion(
                        moduleDef,
                        methodBody,
                        conversion.WrapperTypeSig!,
                        conversion.InnerTypeSig!,
                        context,
                        conversion.IsReverseDuckChaining);
                    return;
                }

                if (conversion.Kind == MethodReturnConversionKind.ExtractDuckTypeInstance)
                {
                    if (conversion.KeepsNull)
                    {
                        var hasValueLabel = Instruction.Create(OpCodes.Nop);
                        var endLabel = Instruction.Create(OpCodes.Nop);
                        methodBody.Instructions.Add(OpCodes.Dup.ToInstruction());
                        methodBody.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValueLabel));
                        methodBody.Instructions.Add(OpCodes.Pop.ToInstruction());
                        methodBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                        methodBody.Instructions.Add(OpCodes.Br_S.ToInstruction(endLabel));
                        methodBody.Instructions.Add(hasValueLabel);
                        methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                        methodBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                        methodBody.Instructions.Add(endLabel);
                    }
                    else
                    {
                        methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                        methodBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                    }

                    EmitObjectToExpectedTypeConversion(moduleDef, methodBody, conversion.InnerTypeSig!, context);
                }
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.EmitMethodReturnConversionSeconds += seconds);
            }
        }

        /// <summary>
        /// Emits method argument conversion.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="methodBody">The method body value.</param>
        /// <param name="conversion">The conversion value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="context">The context value.</param>
        /// <param name="preserveNullForDuckTypeExtraction">Whether null duck-chain inputs should be preserved instead of dereferenced.</param>
        private static void EmitMethodArgumentConversion(
            ModuleDef moduleDef,
            CilBody methodBody,
            MethodArgumentConversion conversion,
            ImportedMembers importedMembers,
            string context,
            bool preserveNullForDuckTypeExtraction)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                if (_currentProfile is not null)
                {
                    switch (conversion.Kind)
                    {
                        case MethodArgumentConversionKind.None:
                            _currentProfile.EmitArgumentConversionNoneCount++;
                            break;
                        case MethodArgumentConversionKind.UnwrapValueWithType:
                            _currentProfile.EmitArgumentConversionUnwrapCount++;
                            break;
                        case MethodArgumentConversionKind.ExtractDuckTypeInstance:
                            _currentProfile.EmitArgumentConversionExtractDuckTypeCount++;
                            break;
                        case MethodArgumentConversionKind.DuckChainToProxy:
                            _currentProfile.EmitArgumentConversionDuckChainCount++;
                            break;
                        case MethodArgumentConversionKind.TypeConversion:
                            _currentProfile.EmitArgumentConversionTypeConversionCount++;
                            break;
                    }
                }

                if (conversion.RequiresValueWithTypeUnwrap)
                {
                    var valueFieldRef = CreateValueWithTypeValueFieldRef(
                        moduleDef,
                        conversion.UnwrapWrapperTypeSig!,
                        conversion.UnwrapInnerTypeSig!);
                    methodBody.Instructions.Add(OpCodes.Ldfld.ToInstruction(valueFieldRef));
                }

                switch (conversion.Kind)
                {
                    case MethodArgumentConversionKind.None:
                        return;
                    case MethodArgumentConversionKind.UnwrapValueWithType:
                    {
                        var valueFieldRef = CreateValueWithTypeValueFieldRef(moduleDef, conversion.WrapperTypeSig!, conversion.InnerTypeSig!);
                        methodBody.Instructions.Add(OpCodes.Ldfld.ToInstruction(valueFieldRef));
                        return;
                    }

                    case MethodArgumentConversionKind.ExtractDuckTypeInstance:
                        if (preserveNullForDuckTypeExtraction)
                        {
                            // Dynamic duck typing treats null ref/out inputs as null instances, not as invalid IDuckType.
                            var hasValueLabel = Instruction.Create(OpCodes.Nop);
                            var endLabel = Instruction.Create(OpCodes.Nop);
                            methodBody.Instructions.Add(OpCodes.Dup.ToInstruction());
                            methodBody.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValueLabel));
                            methodBody.Instructions.Add(OpCodes.Pop.ToInstruction());
                            methodBody.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                            methodBody.Instructions.Add(OpCodes.Br_S.ToInstruction(endLabel));
                            methodBody.Instructions.Add(hasValueLabel);
                            methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                            methodBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                            methodBody.Instructions.Add(endLabel);
                        }
                        else
                        {
                            methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedMembers.IDuckTypeType));
                            methodBody.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.IDuckTypeInstanceGetter));
                        }

                        EmitObjectToExpectedTypeConversion(moduleDef, methodBody, conversion.InnerTypeSig!, context);
                        return;
                    case MethodArgumentConversionKind.DuckChainToProxy:
                        EmitDuckChainToProxyConversion(moduleDef, methodBody, conversion.WrapperTypeSig!, conversion.InnerTypeSig!, context);
                        return;
                    case MethodArgumentConversionKind.TypeConversion:
                        EmitTypeConversion(moduleDef, methodBody, conversion.WrapperTypeSig!, conversion.InnerTypeSig!, context);
                        return;
                    default:
                        throw new InvalidOperationException($"Unsupported method argument conversion '{conversion.Kind}'.");
                }
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.EmitMethodArgumentConversionSeconds += seconds);
            }
        }

        /// <summary>
        /// Emits load by ref value.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="methodBody">The method body value.</param>
        /// <param name="valueTypeSig">The value type sig value.</param>
        /// <param name="context">The context value.</param>
        private static void EmitLoadByRefValue(ModuleDef moduleDef, CilBody methodBody, TypeSig valueTypeSig, string context)
        {
            var importedValueType = ResolveImportedTypeForTypeToken(moduleDef, valueTypeSig, context);
            methodBody.Instructions.Add(OpCodes.Ldobj.ToInstruction(importedValueType));
        }

        /// <summary>
        /// Emits store by ref value.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="methodBody">The method body value.</param>
        /// <param name="valueTypeSig">The value type sig value.</param>
        /// <param name="context">The context value.</param>
        private static void EmitStoreByRefValue(ModuleDef moduleDef, CilBody methodBody, TypeSig valueTypeSig, string context)
        {
            var importedValueType = ResolveImportedTypeForTypeToken(moduleDef, valueTypeSig, context);
            methodBody.Instructions.Add(OpCodes.Stobj.ToInstruction(importedValueType));
        }

        /// <summary>
        /// Emits type conversion.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="methodBody">The method body value.</param>
        /// <param name="actualTypeSig">The actual type sig value.</param>
        /// <param name="expectedTypeSig">The expected type sig value.</param>
        /// <param name="context">The context value.</param>
        private static void EmitTypeConversion(
            ModuleDef moduleDef,
            CilBody methodBody,
            TypeSig actualTypeSig,
            TypeSig expectedTypeSig,
            string context)
        {
            if (actualTypeSig.IsGenericParameter && expectedTypeSig.IsGenericParameter)
            {
                return;
            }

            var actualUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(actualTypeSig);
            var expectedUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(expectedTypeSig);
            if (AreTypesEquivalent(actualUnderlyingTypeSig, expectedUnderlyingTypeSig))
            {
                return;
            }

            if (actualUnderlyingTypeSig.IsValueType)
            {
                if (expectedUnderlyingTypeSig.IsValueType)
                {
                    throw new InvalidOperationException($"Unsupported value-type conversion from '{actualTypeSig.FullName}' to '{expectedTypeSig.FullName}' in {context}.");
                }

                var importedActualType = ResolveImportedTypeForTypeToken(moduleDef, actualTypeSig, context);
                methodBody.Instructions.Add(OpCodes.Box.ToInstruction(importedActualType));
                if (!IsObjectTypeSig(expectedUnderlyingTypeSig))
                {
                    var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedUnderlyingTypeSig, context);
                    methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedExpectedType));
                }

                return;
            }

            if (expectedUnderlyingTypeSig.IsValueType)
            {
                var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedTypeSig, context);
                var isExpectedLabel = Instruction.Create(OpCodes.Nop);
                methodBody.Instructions.Add(OpCodes.Dup.ToInstruction());
                methodBody.Instructions.Add(OpCodes.Isinst.ToInstruction(importedExpectedType));
                methodBody.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(isExpectedLabel));
                methodBody.Instructions.Add(OpCodes.Pop.ToInstruction());
                var invalidCastExceptionCtor = typeof(InvalidCastException).GetConstructor(Type.EmptyTypes)
                                            ?? throw new InvalidOperationException("Unable to resolve InvalidCastException::.ctor().");
                methodBody.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(invalidCastExceptionCtor)));
                methodBody.Instructions.Add(OpCodes.Throw.ToInstruction());
                methodBody.Instructions.Add(isExpectedLabel);
                methodBody.Instructions.Add(OpCodes.Unbox_Any.ToInstruction(importedExpectedType));
                return;
            }

            if (!IsObjectTypeSig(expectedUnderlyingTypeSig))
            {
                var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedUnderlyingTypeSig, context);
                methodBody.Instructions.Add(OpCodes.Castclass.ToInstruction(importedExpectedType));
            }
        }

        /// <summary>
        /// Emits i duck type implementation.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="generatedType">The generated type value.</param>
        /// <param name="importedTargetType">The imported target type value.</param>
        /// <param name="targetField">The target field value.</param>
        /// <param name="importedMembers">The imported members value.</param>
        /// <param name="targetIsValueType">The target is value type value.</param>
        /// <param name="targetToString">The public ToString() of the target type the proxy's ToString calls, if any.</param>
        /// <param name="targetType">The target type definition.</param>
        /// <param name="closedGenericTargetTypeArguments">The generic arguments of a closed generic target type.</param>
        private static void EmitIDuckTypeImplementation(
            ModuleDef moduleDef,
            TypeDef generatedType,
            ITypeDefOrRef importedTargetType,
            FieldDef targetField,
            ImportedMembers importedMembers,
            bool targetIsValueType,
            MethodDef? targetToString,
            TypeDef targetType,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            FieldDef? reportedTargetTypeField = null)
        {
            // Emit IDuckType.Instance once per generated type.
            if (generatedType.FindMethod("get_Instance") is null)
            {
                var getInstanceMethod = new MethodDefUser(
                    "get_Instance",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.Object),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                getInstanceMethod.Body = new CilBody();
                getInstanceMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                getInstanceMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(targetField));
                // IDuckType.Instance is object-typed, so value-type targets must be boxed here.
                if (targetIsValueType)
                {
                    getInstanceMethod.Body.Instructions.Add(OpCodes.Box.ToInstruction(importedTargetType));
                }

                getInstanceMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(getInstanceMethod);

                var instanceProperty = new PropertyDefUser("Instance", new PropertySig(hasThis: true, moduleDef.CorLibTypes.Object));
                instanceProperty.GetMethod = getInstanceMethod;
                generatedType.Properties.Add(instanceProperty);
            }

            // Emit IDuckType.Type once per generated type.
            if (generatedType.FindMethod("get_Type") is null)
            {
                var getTypeMethod = new MethodDefUser(
                    "get_Type",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.GetTypeRef("System", "Type").ToTypeSig()),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.SpecialName);
                getTypeMethod.Body = new CilBody();
                var returnType = OpCodes.Ret.ToInstruction();
                if (reportedTargetTypeField is not null)
                {
                    // The type the proxy is created for, when it isn't the target type of the mapping.
                    getTypeMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                    getTypeMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(reportedTargetTypeField));
                    getTypeMethod.Body.Instructions.Add(OpCodes.Dup.ToInstruction());
                    getTypeMethod.Body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(returnType));
                    getTypeMethod.Body.Instructions.Add(OpCodes.Pop.ToInstruction());
                }

                getTypeMethod.Body.Instructions.Add(OpCodes.Ldtoken.ToInstruction(importedTargetType));
                getTypeMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(importedMembers.GetTypeFromHandleMethod));
                getTypeMethod.Body.Instructions.Add(returnType);
                generatedType.Methods.Add(getTypeMethod);

                var typeProperty = new PropertyDefUser("Type", new PropertySig(hasThis: true, moduleDef.CorLibTypes.GetTypeRef("System", "Type").ToTypeSig()));
                typeProperty.GetMethod = getTypeMethod;
                generatedType.Properties.Add(typeProperty);
            }

            // Emit ref-return helper used by runtime conversion paths.
            if (generatedType.FindMethod("GetInternalDuckTypedInstance") is null)
            {
                var getInternalInstanceMethod = new MethodDefUser(
                    "GetInternalDuckTypedInstance",
                    MethodSig.CreateInstanceGeneric(1, new ByRefSig(new GenericMVar(0))),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot);
                getInternalInstanceMethod.GenericParameters.Add(new GenericParamUser(0, GenericParamAttributes.NonVariant, "TReturn"));
                getInternalInstanceMethod.Body = new CilBody();
                getInternalInstanceMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                getInternalInstanceMethod.Body.Instructions.Add(OpCodes.Ldflda.ToInstruction(targetField));
                getInternalInstanceMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(getInternalInstanceMethod);
            }

            // Like dynamic duck typing, ToString has the attributes of the target's public ToString() and calls it: it overrides
            // object.ToString only when that method overrides it, and it implements IDuckType.ToString when it's virtual.
            const MethodAttributes ToStringAttributes = MethodAttributes.MemberAccessMask | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.VtableLayoutMask | MethodAttributes.CheckAccessOnOverride;
            if (targetToString is { } uncallableToString &&
                (uncallableToString.IsStatic || uncallableToString.MethodSig.GenParamCount > 0) &&
                generatedType.FindMethod("ToString") is null)
            {
                // A static ToString(), or a generic one: the proxy gets a ToString with its attributes, which dynamic duck typing's
                // calls the method found. A static or non-virtual one isn't called (the proxy has no ToString of its own); a virtual
                // generic one implements IDuckType.ToString (or overrides object.ToString), and calling the generic method
                // definition fails like it does for dynamic duck typing's.
                var stubMethod = new MethodDefUser(
                    "ToString",
                    uncallableToString.IsStatic ? MethodSig.CreateStatic(moduleDef.CorLibTypes.String) : MethodSig.CreateInstance(moduleDef.CorLibTypes.String),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    uncallableToString.Attributes & (ToStringAttributes | MethodAttributes.Static));
                stubMethod.Body = new CilBody();
                if (uncallableToString.IsVirtual)
                {
                    // The BadImageFormatException of the runtime (COR_E_BADIMAGEFORMAT), with its message on that platform.
                    stubMethod.Body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(unchecked((int)0x8007000B)));
                    stubMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(RuntimeImporter(moduleDef).Import(typeof(Marshal).GetMethod(nameof(Marshal.GetExceptionForHR), [typeof(int)])!)));
                    stubMethod.Body.Instructions.Add(OpCodes.Throw.ToInstruction());
                }
                else
                {
                    stubMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                    stubMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                }

                generatedType.Methods.Add(stubMethod);
            }
            else if (targetToString is not null && generatedType.FindMethod("ToString") is null)
            {
                var toStringMethod = new MethodDefUser(
                    "ToString",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.String),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    targetToString.Attributes & ToStringAttributes);
                toStringMethod.Body = new CilBody();

                // An override of object.ToString is reached through its slot. Another method (one that hides object.ToString, or an
                // override of such a method) is called directly: one of a type of the registry (a generated reverse proxy) as it
                // is, another on the closed type for a generic declaring type.
                IMethod calledToString;
                if (targetToString.IsVirtual &&
                    !targetToString.IsNewSlot &&
                    GetVtableSlotMethod(targetToString.Module == moduleDef ? targetToString.DeclaringType : targetType, targetToString).DeclaringType is { BaseType: null, IsInterface: false } slotType &&
                    string.Equals(slotType.FullName, "System.Object", StringComparison.Ordinal))
                {
                    calledToString = importedMembers.ObjectToStringMethod;
                }
                else if (targetToString.Module == moduleDef)
                {
                    calledToString = targetToString;
                }
                else
                {
                    calledToString = CreateMethodCallTarget(
                        moduleDef,
                        ImportMethodDefOrRefCached(moduleDef, targetToString, $"ToString of target type '{targetToString.DeclaringType.FullName}'"),
                        ImportMemberDeclaringType(moduleDef, targetType, targetToString.DeclaringType, closedGenericTargetTypeArguments),
                        toStringMethod,
                        closedGenericMethodArguments: null);
                }

                // Value-type targets use constrained callvirt to avoid boxing while preserving virtual dispatch semantics.
                if (targetIsValueType)
                {
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldflda.ToInstruction(targetField));
                    toStringMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(importedTargetType));
                    toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(calledToString));
                }
                else
                {
                    // Reference-type targets preserve null behavior by returning null when _instance is null.
                    var hasValueLabel = Instruction.Create(OpCodes.Nop);
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(targetField));
                    toStringMethod.Body.Instructions.Add(OpCodes.Dup.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValueLabel));
                    toStringMethod.Body.Instructions.Add(OpCodes.Pop.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                    toStringMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                    toStringMethod.Body.Instructions.Add(hasValueLabel);
                    toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(calledToString));
                }

                toStringMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(toStringMethod);
            }
            else if (targetToString is null &&
                     reportedTargetTypeField is not null &&
                     generatedType.BaseType is { } baseType &&
                     generatedType.FindMethod("ToString") is null)
            {
                // A target type without a public ToString() (an interface): the proxy of the target type itself has no ToString,
                // dynamic duck typing's proxy of a class this proxy serves (see DuckTypeAotEngine.TryGetFallbackResult) has the one
                // of object.ToString (it implements IDuckType.ToString and calls the instance's). The ToString this one doesn't
                // have is the inherited one: object.ToString of the boxed value of a struct (this method doesn't override it).
                var toStringMethod = new MethodDefUser(
                    "ToString",
                    MethodSig.CreateInstance(moduleDef.CorLibTypes.String),
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot);
                toStringMethod.Body = new CilBody();
                var servedTypeLabel = Instruction.Create(OpCodes.Nop);
                var hasValueLabel = Instruction.Create(OpCodes.Nop);
                toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                toStringMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(reportedTargetTypeField));
                toStringMethod.Body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(servedTypeLabel));
                toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                if (generatedType.IsValueType)
                {
                    toStringMethod.Body.Instructions.Add(OpCodes.Constrained.ToInstruction(generatedType));
                    toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.ObjectToStringMethod));
                }
                else
                {
                    toStringMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(new MemberRefUser(moduleDef, "ToString", MethodSig.CreateInstance(moduleDef.CorLibTypes.String), baseType)));
                }

                toStringMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                toStringMethod.Body.Instructions.Add(servedTypeLabel);
                toStringMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                toStringMethod.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(targetField));
                toStringMethod.Body.Instructions.Add(OpCodes.Dup.ToInstruction());
                toStringMethod.Body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValueLabel));
                toStringMethod.Body.Instructions.Add(OpCodes.Pop.ToInstruction());
                toStringMethod.Body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                toStringMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                toStringMethod.Body.Instructions.Add(hasValueLabel);
                toStringMethod.Body.Instructions.Add(OpCodes.Callvirt.ToInstruction(importedMembers.ObjectToStringMethod));
                toStringMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                generatedType.Methods.Add(toStringMethod);
            }
        }

        /// <summary>
        /// Finds the public ToString() of a target type, like the Type.GetMethod("ToString", Type.EmptyTypes) dynamic duck typing
        /// defines the proxy's ToString from: the most derived one, generic method definitions too; a static one only when the
        /// type itself declares it; for an interface, only one the interface itself declares.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <returns>The ToString method, or null if the target type has none.</returns>
        private static MethodDef? FindPublicToString(TypeDef targetType)
        {
            for (var current = targetType; current is not null; current = current.IsInterface ? null : current.BaseType?.ResolveTypeDef())
            {
                foreach (var method in current.Methods)
                {
                    if (method.IsPublic &&
                        method.MethodSig.Params.Count == 0 &&
                        string.Equals(method.Name, "ToString", StringComparison.Ordinal) &&
                        (!method.IsStatic || ReferenceEquals(current, targetType)))
                    {
                        return method;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Ensures emitted interface accessors are also represented as property metadata when possible.
        /// </summary>
        /// <param name="moduleDef">The module definition.</param>
        /// <param name="generatedType">The generated type.</param>
        /// <param name="proxyMethod">Source accessor method from the proxy definition.</param>
        /// <param name="generatedMethod">Generated accessor method on the emitted proxy type.</param>
        /// <param name="generatedPropertiesByKey">Per-type cache to avoid duplicating emitted property rows.</param>
        /// <param name="implementationProperty">For a reverse proxy, the delegation's property implementing the accessor.</param>
        private static void EnsureInterfacePropertyMetadata(
            ModuleDef moduleDef,
            TypeDef generatedType,
            MethodDef proxyMethod,
            MethodDef generatedMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IDictionary<string, PropertyDef> generatedPropertiesByKey,
            PropertyDef? implementationProperty = null)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                // Keeping property rows aligned with emitted accessors preserves reflection and decompiler behavior parity.
                // Without this, proxies may appear to expose only get_/set_ methods in downstream tooling.
                if (!proxyMethod.IsSpecialName)
                {
                    return;
                }

                var accessorName = proxyMethod.Name;
                var isGetter = accessorName.StartsWith("get_", StringComparison.Ordinal);
                var isSetter = accessorName.StartsWith("set_", StringComparison.Ordinal);
                if (!isGetter && !isSetter)
                {
                    return;
                }

                var proxyProperty = FindPropertyFromAccessor(proxyMethod);
                string propertyKey;
                string propertyName;
                PropertySig importedPropertySig;
                // Prefer declared property metadata when available.
                if (proxyProperty is not null)
                {
                    importedPropertySig = CreateImportedPropertySig(moduleDef, proxyProperty.PropertySig, closedGenericProxyTypeArguments);
                    propertyName = proxyProperty.Name;
                    propertyKey = $"{BuildPropertyIdentityKey(proxyProperty)}::{BuildTypeSigSequenceCacheKey(closedGenericProxyTypeArguments)}";
                }
                else
                {
                    // Fallback keeps metadata usable for contracts that declare accessor methods without property rows.
                    if (!TryInferPropertyMetadataFromAccessor(moduleDef, proxyMethod, closedGenericProxyTypeArguments, out propertyName, out importedPropertySig, out propertyKey))
                    {
                        return;
                    }
                }

                if (implementationProperty is not null)
                {
                    // Like dynamic duck typing's: the name of the delegation's property, the type of the implemented one (without
                    // index parameters).
                    propertyName = implementationProperty.Name;
                    importedPropertySig = PropertySig.CreateInstance(importedPropertySig.RetType);
                }

                // Create property row once, then attach getter/setter accessors as they are emitted.
                if (!generatedPropertiesByKey.TryGetValue(propertyKey, out var generatedProperty))
                {
                    // One property definition per logical key avoids duplicate property rows in generated metadata.
                    generatedProperty = new PropertyDefUser(propertyName, importedPropertySig);
                    generatedType.Properties.Add(generatedProperty);
                    generatedPropertiesByKey[propertyKey] = generatedProperty;
                }

                if (isGetter)
                {
                    generatedProperty.GetMethod ??= generatedMethod;
                }
                else
                {
                    generatedProperty.SetMethod ??= generatedMethod;
                }
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.EnsureInterfacePropertyMetadataSeconds += seconds);
            }
        }

        /// <summary>
        /// Infers property metadata from accessor-shaped methods when declaring property metadata is unavailable.
        /// </summary>
        /// <param name="moduleDef">The destination module definition.</param>
        /// <param name="accessorMethod">The accessor method.</param>
        /// <param name="propertyName">The inferred property name.</param>
        /// <param name="propertySig">The inferred property signature.</param>
        /// <param name="propertyKey">The inferred property key.</param>
        /// <returns>
        /// <see langword="true"/> when a property name and signature can be inferred from the accessor method shape;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        private static bool TryInferPropertyMetadataFromAccessor(
            ModuleDef moduleDef,
            MethodDef accessorMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            out string propertyName,
            out PropertySig propertySig,
            out string propertyKey)
        {
            propertyName = string.Empty;
            propertySig = null!;
            propertyKey = string.Empty;

            var accessorName = accessorMethod.Name;
            // Getter pattern: property value is return type, all parameters are indexer parameters.
            if (accessorName.StartsWith("get_", StringComparison.Ordinal))
            {
                // Invalid getter shape: getters cannot return void.
                if (accessorMethod.MethodSig.RetType.ElementType == ElementType.Void)
                {
                    return false;
                }

                propertyName = accessorName.Substring(4);
                var importedReturnType = ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(accessorMethod.MethodSig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"getter return type '{accessorMethod.FullName}'");
                var importedParameterTypes = accessorMethod.MethodSig.Params.Select(parameterType => ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(parameterType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"getter parameter type '{accessorMethod.FullName}'")).ToArray();
                propertySig = new PropertySig(hasThis: true, importedReturnType, importedParameterTypes);
                propertyKey = $"{(accessorMethod.DeclaringType is null ? string.Empty : BuildTypeIdentityKey(accessorMethod.DeclaringType))}::{propertyName}::{BuildPropertySignatureCacheKey(propertySig)}";
                return true;
            }

            // Non-getter and non-setter methods cannot describe a property.
            if (!accessorName.StartsWith("set_", StringComparison.Ordinal))
            {
                return false;
            }

            // Valid setter shape: void return and at least one parameter (the value parameter).
            if (accessorMethod.MethodSig.RetType.ElementType != ElementType.Void || accessorMethod.MethodSig.Params.Count == 0)
            {
                return false;
            }

            propertyName = accessorName.Substring(4);
            var valueType = ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(accessorMethod.MethodSig.Params[accessorMethod.MethodSig.Params.Count - 1], closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"setter value type '{accessorMethod.FullName}'");
            var importedIndexParameters = accessorMethod.MethodSig.Params
                                            .Take(accessorMethod.MethodSig.Params.Count - 1)
                                            .Select(parameterType => ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(parameterType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"setter parameter type '{accessorMethod.FullName}'"))
                                            .ToArray();
            propertySig = new PropertySig(hasThis: true, valueType, importedIndexParameters);
            propertyKey = $"{(accessorMethod.DeclaringType is null ? string.Empty : BuildTypeIdentityKey(accessorMethod.DeclaringType))}::{propertyName}::{BuildPropertySignatureCacheKey(propertySig)}";
            return true;
        }

        /// <summary>
        /// Creates an imported property signature for the generated module.
        /// </summary>
        /// <param name="moduleDef">The destination module definition.</param>
        /// <param name="sourcePropertySig">The source property signature.</param>
        /// <returns>The imported property signature.</returns>
        private static PropertySig CreateImportedPropertySig(ModuleDef moduleDef, PropertySig sourcePropertySig, IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments)
        {
            var cacheKey = string.Concat(
                BuildPropertySignatureCacheKey(sourcePropertySig),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericProxyTypeArguments));
            if (_currentExecutionContext?.TryGetPropertySignature(cacheKey, out var cachedPropertySig) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.PropertySignatureCacheHits++;
                }

                return cachedPropertySig!;
            }

            if (_currentProfile is not null)
            {
                _currentProfile.PropertySignatureCacheMisses++;
            }

            var importedReturnType = ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(sourcePropertySig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"property signature return type '{sourcePropertySig}'");
            PropertySig importedPropertySig;
            if (sourcePropertySig.Params.Count == 0)
            {
                importedPropertySig = new PropertySig(hasThis: sourcePropertySig.HasThis, importedReturnType);
                _currentExecutionContext?.CachePropertySignature(cacheKey, importedPropertySig);
                return importedPropertySig;
            }

            var importedParameterTypes = new TypeSig[sourcePropertySig.Params.Count];
            for (var index = 0; index < sourcePropertySig.Params.Count; index++)
            {
                importedParameterTypes[index] = ImportTypeSigCached(moduleDef, SubstituteTypeAndMethodGenericTypeArguments(sourcePropertySig.Params[index], closedGenericProxyTypeArguments, closedGenericMethodArguments: null), $"property signature parameter '{sourcePropertySig}'");
            }

            importedPropertySig = new PropertySig(hasThis: sourcePropertySig.HasThis, importedReturnType, importedParameterTypes);
            _currentExecutionContext?.CachePropertySignature(cacheKey, importedPropertySig);
            return importedPropertySig;
        }

        /// <summary>
        /// Gets interface method attributes.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static MethodAttributes GetInterfaceMethodAttributes(MethodDef proxyMethod)
        {
            // Like dynamic duck typing, without NewSlot: a method implementing an Equals, GetHashCode or ToString declared by the
            // proxy interface also overrides the object method.
            var attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final;
            if (proxyMethod.IsSpecialName)
            {
                attributes |= MethodAttributes.SpecialName;
            }

            if (proxyMethod.IsRuntimeSpecialName)
            {
                attributes |= MethodAttributes.RTSpecialName;
            }

            return attributes;
        }

        /// <summary>
        /// Adds an explicit interface method override for inherited non-generic interface methods.
        /// </summary>
        /// <param name="moduleDef">The module definition value.</param>
        /// <param name="generatedType">The generated type value.</param>
        /// <param name="generatedInterfaceImplementations">The generated interface implementation keys.</param>
        /// <param name="isInterfaceProxy">The is interface proxy value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="generatedMethod">The generated method value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="interfaceMethodContract">The interface contract that declared the method.</param>
        /// <param name="closedGenericProxyTypeArguments">The closed proxy generic type arguments.</param>
        private static void AddInterfaceMethodOverride(
            ModuleDef moduleDef,
            TypeDef generatedType,
            HashSet<string> generatedInterfaceImplementations,
            bool isInterfaceProxy,
            TypeDef proxyType,
            MethodDef generatedMethod,
            MethodDef proxyMethod,
            InterfaceMethodContract? interfaceMethodContract,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments)
        {
            if (!isInterfaceProxy ||
                interfaceMethodContract is null ||
                !interfaceMethodContract.RequiresExplicitInterfaceImplementation)
            {
                return;
            }

            var inheritedInterface = ImportInterfaceContractTypeDefOrRef(
                moduleDef,
                interfaceMethodContract,
                closedGenericProxyTypeArguments,
                $"inherited interface contract '{interfaceMethodContract.InterfaceType.FullName}'");
            if (generatedInterfaceImplementations.Add(BuildTypeDefOrRefIdentityKey(inheritedInterface)))
            {
                generatedType.Interfaces.Add(new InterfaceImplUser(inheritedInterface));
            }

            var importedProxyMethodSig = CreateImportedInterfaceContractMethodSig(
                moduleDef,
                interfaceMethodContract.MethodSig,
                closedGenericProxyTypeArguments,
                $"inherited interface method '{proxyMethod.FullName}'");
            var importedProxyMethod = new MemberRefUser(
                moduleDef,
                proxyMethod.Name,
                importedProxyMethodSig,
                inheritedInterface);
            generatedMethod.Overrides.Add(new MethodOverride(generatedMethod, moduleDef.UpdateRowId(importedProxyMethod)));
        }

        /// <summary>
        /// Gets the attributes of the method overriding a method of a class proxy or reverse proxy, like dynamic duck typing defines
        /// them: final, public (property accessors too), except the override of a protected method in a reverse proxy.
        /// </summary>
        /// <param name="proxyMethod">The overridden method.</param>
        /// <param name="isReverseMapping">Whether the proxy is a reverse proxy.</param>
        /// <returns>The attributes of the override.</returns>
        private static MethodAttributes GetClassOverrideMethodAttributes(MethodDef proxyMethod, bool isReverseMapping)
        {
            var isPropertyAccessor = proxyMethod.IsSpecialName && FindPropertyFromAccessor(proxyMethod) is not null;
            var memberAccess = isReverseMapping && !isPropertyAccessor && proxyMethod.IsFamily ? MethodAttributes.Family : MethodAttributes.Public;
            var attributes = memberAccess | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.ReuseSlot;
            return isPropertyAccessor ? attributes | MethodAttributes.SpecialName : attributes;
        }

        /// <summary>
        /// Finds a supported parameterless base constructor for class proxy emission.
        /// </summary>
        /// <param name="proxyType">The proxy type value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static MethodDef? FindSupportedProxyBaseConstructor(TypeDef proxyType)
        {
            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyType);
            return proxyTypePlan?.SupportedBaseConstructor;
        }

        /// <summary>
        /// Creates duck type create cache create method ref.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="proxyTypeSig">The proxy type sig value.</param>
        /// <param name="reverse">Whether to call the reverse factory.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethodDefOrRef CreateDuckTypeCreateCacheCreateMethodRef(ModuleDef moduleDef, TypeSig proxyTypeSig, bool reverse = false)
        {
            var cacheKey = BuildTypeSigCacheKey(proxyTypeSig) + (reverse ? "::reverse" : "::forward");
            if (_currentExecutionContext?.TryGetDuckTypeCreateCacheCreateMethodRef(cacheKey, out var cachedMethod) == true)
            {
                return cachedMethod!;
            }

            var importedProxyTypeSig = ImportTypeSigCached(moduleDef, proxyTypeSig, $"DuckType.CreateCache.Create proxy '{proxyTypeSig.FullName}'");
            var importedCreateCacheOpenType = ImportRuntimeTypeCached(moduleDef, typeof(DuckType.CreateCache<>), "DuckType.CreateCache<> type");

            var importedCreateCacheOpenTypeSig = importedCreateCacheOpenType.ToTypeSig() as ClassOrValueTypeSig
                ?? throw new InvalidOperationException("Unable to resolve DuckType.CreateCache<> signature.");

            var createCacheClosedTypeSig = new GenericInstSig(importedCreateCacheOpenTypeSig, importedProxyTypeSig);
            var createCacheClosedTypeSpec = moduleDef.UpdateRowId(new TypeSpecUser(createCacheClosedTypeSig));
            var createMethodSig = MethodSig.CreateStatic(new GenericVar(0), moduleDef.CorLibTypes.Object);
            var createMethodRef = new MemberRefUser(moduleDef, reverse ? "CreateReverse" : "Create", createMethodSig, createCacheClosedTypeSpec);
            var importedMethod = moduleDef.UpdateRowId(createMethodRef);
            _currentExecutionContext?.CacheDuckTypeCreateCacheCreateMethodRef(cacheKey, importedMethod);
            return importedMethod;
        }

        private static IMethod CreateDuckTypeCreateCacheCreateFromMethodRef(ModuleDef moduleDef, TypeSig proxyTypeSig, TypeSig targetTypeSig)
        {
            var cacheKey = string.Concat(BuildTypeSigCacheKey(proxyTypeSig), "|", BuildTypeSigCacheKey(targetTypeSig));
            if (_currentExecutionContext?.TryGetDuckTypeCreateCacheCreateFromMethodRef(cacheKey, out var cachedMethod) == true)
            {
                return cachedMethod!;
            }

            var importedProxyTypeSig = ImportTypeSigCached(moduleDef, proxyTypeSig, $"DuckType.CreateCache.CreateFrom proxy '{proxyTypeSig.FullName}'");
            var importedTargetTypeSig = ImportTypeSigCached(moduleDef, targetTypeSig, $"DuckType.CreateCache.CreateFrom target '{targetTypeSig.FullName}'");
            var importedCreateCacheOpenType = ImportRuntimeTypeCached(moduleDef, typeof(DuckType.CreateCache<>), "DuckType.CreateCache<> type");

            var importedCreateCacheOpenTypeSig = importedCreateCacheOpenType.ToTypeSig() as ClassOrValueTypeSig
                ?? throw new InvalidOperationException("Unable to resolve DuckType.CreateCache<> signature.");

            var createCacheClosedTypeSig = new GenericInstSig(importedCreateCacheOpenTypeSig, importedProxyTypeSig);
            var createCacheClosedTypeSpec = moduleDef.UpdateRowId(new TypeSpecUser(createCacheClosedTypeSig));
            var createFromMethodSig = MethodSig.CreateStaticGeneric(1, new GenericVar(0), new GenericMVar(0));
            var createFromMethodRef = new MemberRefUser(moduleDef, "CreateFrom", createFromMethodSig, createCacheClosedTypeSpec);
            var createFromMethodSpec = new MethodSpecUser(createFromMethodRef, new GenericInstMethodSig(importedTargetTypeSig));
            var importedMethod = moduleDef.UpdateRowId(createFromMethodSpec);
            _currentExecutionContext?.CacheDuckTypeCreateCacheCreateFromMethodRef(cacheKey, importedMethod);
            return importedMethod;
        }

        /// <summary>
        /// Gets the message of the DuckTypeException dynamic duck typing throws when Reflection.Emit can't create a proxy type.
        /// </summary>
        /// <param name="targetTypeName">The reflection name of the target type.</param>
        /// <param name="proxyTypeName">The reflection name of the proxy type.</param>
        /// <returns>The message.</returns>
        private static string CreateDuckTypeCreationFailureMessage(string targetTypeName, string proxyTypeName)
            => $"{CreatingDuckTypeFailurePrefix}'{DuckTypeAotNameHelpers.ToTypeToStringName(targetTypeName)}' using proxy: '{DuckTypeAotNameHelpers.ToTypeToStringName(proxyTypeName)}'";

        private static void EmitTrailingOptionalTargetArguments(
            ModuleDef moduleDef,
            CilBody body,
            MethodDef targetMethod,
            ForwardMethodBindingInfo methodBinding,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            if (methodBinding.TrailingOptionalTargetParameterCount == 0)
            {
                return;
            }

            var firstOptionalParameterIndex = targetMethod.MethodSig.Params.Count - methodBinding.TrailingOptionalTargetParameterCount;
            for (var parameterIndex = firstOptionalParameterIndex; parameterIndex < targetMethod.MethodSig.Params.Count; parameterIndex++)
            {
                var parameterType = SubstituteTypeAndMethodGenericTypeArguments(
                    targetMethod.MethodSig.Params[parameterIndex],
                    closedGenericTargetTypeArguments,
                    methodBinding.ClosedGenericMethodArguments);
                EmitOptionalParameterDefault(moduleDef, body, targetMethod, parameterIndex, parameterType);
            }
        }

        private static void EmitOptionalParameterDefault(
            ModuleDef moduleDef,
            CilBody body,
            MethodDef targetMethod,
            int parameterIndex,
            TypeSig parameterType)
        {
            var context = $"optional parameter '{parameterIndex.ToString(CultureInfo.InvariantCulture)}' of method '{targetMethod.FullName}'";

            // Optional by-ref parameters (e.g. `in int value = 5`) need a storage location, not a null managed pointer.
            // Roslyn wraps the by-ref of `in` parameters of virtual and interface methods in modreq(InAttribute), so look
            // through modifiers; the call itself keeps the target method's original signature.
            if (parameterType.RemoveModifiers() is ByRefSig byRefSig)
            {
                var elementType = byRefSig.Next;
                var valueLocal = new Local(ImportTypeSigCached(moduleDef, elementType, context));
                body.Variables.Add(valueLocal);
                if (TryEmitOptionalParameterValue(moduleDef, body, targetMethod, parameterIndex, elementType))
                {
                    body.Instructions.Add(OpCodes.Stloc.ToInstruction(valueLocal));
                }
                else
                {
                    body.Instructions.Add(OpCodes.Ldloca.ToInstruction(valueLocal));
                    body.Instructions.Add(OpCodes.Initobj.ToInstruction(ResolveImportedTypeForTypeToken(moduleDef, elementType, context)));
                }

                body.Instructions.Add(OpCodes.Ldloca.ToInstruction(valueLocal));
                return;
            }

            if (!TryEmitOptionalParameterValue(moduleDef, body, targetMethod, parameterIndex, parameterType))
            {
                EmitDefaultValue(moduleDef, body, parameterType, context);
            }
        }

        /// <summary>
        /// Emits the default value of an omitted optional parameter. Returns false when the value is default(T), which
        /// the caller materializes because it depends on how the value is passed.
        /// </summary>
        private static bool TryEmitOptionalParameterValue(
            ModuleDef moduleDef,
            CilBody body,
            MethodDef targetMethod,
            int parameterIndex,
            TypeSig valueType)
        {
            if (!TryGetOptionalParameterConstant(targetMethod, parameterIndex, out var constantValue))
            {
                // Like the C# compiler and dynamic duck typing, an omitted [IUnknownConstant] or [IDispatchConstant] object
                // parameter receives a wrapper of null.
#pragma warning disable CS0618 // The wrappers of VARIANT marshalling are obsolete, but still what these defaults are
                if (valueType.ElementType == ElementType.Object &&
                    targetMethod.Parameters.FirstOrDefault(parameter => parameter.MethodSigIndex == parameterIndex)?.ParamDef?.CustomAttributes is { } parameterAttributes &&
                    (parameterAttributes.IsDefined("System.Runtime.CompilerServices.IUnknownConstantAttribute") ? typeof(UnknownWrapper) :
                     parameterAttributes.IsDefined("System.Runtime.CompilerServices.IDispatchConstantAttribute") ? typeof(DispatchWrapper) : null) is { } wrapperType)
#pragma warning restore CS0618
                {
                    body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                    body.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(wrapperType.GetConstructor([typeof(object)])!)));
                    return true;
                }

                // Like the C# compiler and dynamic duck typing, an omitted [Optional] object parameter without a default
                // value receives Type.Missing.
                if (valueType.ElementType == ElementType.Object)
                {
                    body.Instructions.Add(OpCodes.Ldsfld.ToInstruction(ImportTypeMissingField(moduleDef)));
                    return true;
                }

                return false;
            }

            if (constantValue is null)
            {
                return false;
            }

            if (TryGetNullableElementType(valueType, out var nullableElementType))
            {
                // The metadata constant of a Nullable<T> parameter is the T value; wrap it instead of passing a raw T.
                if (!TryEmitConstantValue(moduleDef, body, nullableElementType!, constantValue))
                {
                    return false;
                }

                body.Instructions.Add(OpCodes.Newobj.ToInstruction(CreateNullableCtorRef(moduleDef, valueType)));
                return true;
            }

            return TryEmitConstantValue(moduleDef, body, valueType, constantValue);
        }

        private static bool TryEmitConstantValue(ModuleDef moduleDef, CilBody body, TypeSig parameterType, object? constantValue)
        {
            if (constantValue is null)
            {
                EmitDefaultValue(moduleDef, body, parameterType, "optional null parameter");
                return true;
            }

            if (parameterType.ElementType == ElementType.Object)
            {
                TypeSig? boxedType = constantValue switch
                {
                    bool => moduleDef.CorLibTypes.Boolean,
                    char => moduleDef.CorLibTypes.Char,
                    sbyte => moduleDef.CorLibTypes.SByte,
                    byte => moduleDef.CorLibTypes.Byte,
                    short => moduleDef.CorLibTypes.Int16,
                    ushort => moduleDef.CorLibTypes.UInt16,
                    int => moduleDef.CorLibTypes.Int32,
                    uint => moduleDef.CorLibTypes.UInt32,
                    long => moduleDef.CorLibTypes.Int64,
                    ulong => moduleDef.CorLibTypes.UInt64,
                    float => moduleDef.CorLibTypes.Single,
                    double => moduleDef.CorLibTypes.Double,
                    decimal => new ValueTypeSig(moduleDef.CorLibTypes.GetTypeRef("System", "Decimal")),
                    DateTime => new ValueTypeSig(moduleDef.CorLibTypes.GetTypeRef("System", "DateTime")),
                    _ => null
                };
                if (boxedType is not null && TryEmitConstantValue(moduleDef, body, boxedType, constantValue))
                {
                    body.Instructions.Add(OpCodes.Box.ToInstruction(ResolveImportedTypeForTypeToken(moduleDef, boxedType, "boxed optional constant")));
                    return true;
                }
            }

            var underlyingType = GetUnderlyingTypeForTypeConversion(parameterType);

            // The constant of a native integer parameter (nint, nuint) is a 32-bit or 64-bit integer.
            if (underlyingType.ElementType is ElementType.I or ElementType.U && constantValue is sbyte or byte or short or ushort or int or uint or long or ulong)
            {
                body.Instructions.Add(OpCodes.Ldc_I8.ToInstruction(constantValue is ulong or uint or ushort or byte ? unchecked((long)Convert.ToUInt64(constantValue, CultureInfo.InvariantCulture)) : Convert.ToInt64(constantValue, CultureInfo.InvariantCulture)));
                body.Instructions.Add((underlyingType.ElementType == ElementType.I ? OpCodes.Conv_I : OpCodes.Conv_U).ToInstruction());
                return true;
            }

            if (constantValue is decimal decimalValue && TryEmitDecimalConstantValue(moduleDef, body, underlyingType, decimalValue))
            {
                return true;
            }

            if (constantValue is DateTime dateTimeValue && TryEmitDateTimeConstantValue(moduleDef, body, underlyingType, dateTimeValue))
            {
                return true;
            }

            switch (constantValue)
            {
                case bool boolValue:
                    body.Instructions.Add((boolValue ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0).ToInstruction());
                    return true;
                // Widen explicitly: dnlib's sbyte and byte ToInstruction overloads only accept short-form opcodes.
                case char charValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)charValue));
                    return true;
                case sbyte sbyteValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)sbyteValue));
                    return true;
                case byte byteValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)byteValue));
                    return true;
                case short shortValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)shortValue));
                    return true;
                case ushort ushortValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)ushortValue));
                    return true;
                case int intValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(intValue));
                    return true;
                case uint uintValue:
                    body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(unchecked((int)uintValue)));
                    return true;
                case long longValue:
                    body.Instructions.Add(OpCodes.Ldc_I8.ToInstruction(longValue));
                    return true;
                case ulong ulongValue:
                    body.Instructions.Add(OpCodes.Ldc_I8.ToInstruction(unchecked((long)ulongValue)));
                    return true;
                case float floatValue:
                    body.Instructions.Add(OpCodes.Ldc_R4.ToInstruction(floatValue));
                    return true;
                case double doubleValue:
                    body.Instructions.Add(OpCodes.Ldc_R8.ToInstruction(doubleValue));
                    return true;
                case string stringValue:
                    body.Instructions.Add(OpCodes.Ldstr.ToInstruction(stringValue));
                    return true;
            }

            if (underlyingType.ElementType == ElementType.Boolean && TryConvertConstantToLong(constantValue, out var boolNumber))
            {
                body.Instructions.Add((boolNumber != 0 ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0).ToInstruction());
                return true;
            }

            if (TryConvertConstantToLong(constantValue, out var integerValue))
            {
                switch (underlyingType.ElementType)
                {
                    case ElementType.I1:
                    case ElementType.U1:
                    case ElementType.I2:
                    case ElementType.U2:
                    case ElementType.I4:
                    case ElementType.U4:
                    case ElementType.Char:
                        body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(unchecked((int)integerValue)));
                        return true;
                    case ElementType.I8:
                    case ElementType.U8:
                        body.Instructions.Add(OpCodes.Ldc_I8.ToInstruction(integerValue));
                        return true;
                }
            }

            return false;
        }

        private static bool TryEmitDecimalConstantValue(ModuleDef moduleDef, CilBody body, TypeSig parameterType, decimal value)
        {
            if (!IsTypeSigNamed(parameterType, "System.Decimal"))
            {
                return false;
            }

            var bits = decimal.GetBits(value);
            var flags = bits[3];
            var scale = (byte)((flags >> 16) & 0x7F);
            var isNegative = (flags & unchecked((int)0x80000000)) != 0;
            var decimalCtor = typeof(decimal).GetConstructor(new[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(byte) })
                              ?? throw new InvalidOperationException("Unable to resolve decimal constant constructor.");

            body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(bits[0]));
            body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(bits[1]));
            body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction(bits[2]));
            body.Instructions.Add((isNegative ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0).ToInstruction());
            body.Instructions.Add(OpCodes.Ldc_I4.ToInstruction((int)scale));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(decimalCtor)));
            return true;
        }

        private static bool TryEmitDateTimeConstantValue(ModuleDef moduleDef, CilBody body, TypeSig parameterType, DateTime value)
        {
            if (!IsTypeSigNamed(parameterType, "System.DateTime"))
            {
                return false;
            }

            var dateTimeCtor = typeof(DateTime).GetConstructor(new[] { typeof(long) })
                               ?? throw new InvalidOperationException("Unable to resolve DateTime constant constructor.");
            body.Instructions.Add(OpCodes.Ldc_I8.ToInstruction(value.Ticks));
            body.Instructions.Add(OpCodes.Newobj.ToInstruction(RuntimeImporter(moduleDef).Import(dateTimeCtor)));
            return true;
        }

        private static void EmitDefaultValue(ModuleDef moduleDef, CilBody body, TypeSig typeSig, string context)
        {
            // Generic parameters can be instantiated with value types, so only known reference types can use ldnull.
            if (!typeSig.IsValueType && typeSig.RemoveModifiers() is not GenericSig)
            {
                body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
                return;
            }

            var importedTypeSig = ImportTypeSigCached(moduleDef, typeSig, $"default value local for {context}");
            var defaultLocal = new Local(importedTypeSig);
            body.Variables.Add(defaultLocal);
            body.InitLocals = true;
            var importedType = ResolveImportedTypeForTypeToken(moduleDef, typeSig, context);
            body.Instructions.Add(OpCodes.Ldloca.ToInstruction(defaultLocal));
            body.Instructions.Add(OpCodes.Initobj.ToInstruction(importedType));
            body.Instructions.Add(OpCodes.Ldloc.ToInstruction(defaultLocal));
        }

        /// <summary>
        /// Emits object to expected type conversion.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="body">The body value.</param>
        /// <param name="expectedTypeSig">The expected type sig value.</param>
        /// <param name="context">The context value.</param>
        private static void EmitObjectToExpectedTypeConversion(ModuleDef moduleDef, CilBody body, TypeSig expectedTypeSig, string context)
        {
            if (expectedTypeSig.ElementType == ElementType.Object)
            {
                return;
            }

            var importedExpectedType = ResolveImportedTypeForTypeToken(moduleDef, expectedTypeSig, context);
            if (expectedTypeSig.IsValueType)
            {
                body.Instructions.Add(OpCodes.Unbox_Any.ToInstruction(importedExpectedType));
                return;
            }

            body.Instructions.Add(OpCodes.Castclass.ToInstruction(importedExpectedType));
        }

        /// <summary>
        /// Emits duck chain to proxy conversion.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="body">The body value.</param>
        /// <param name="proxyTypeSig">The proxy type sig value.</param>
        /// <param name="targetTypeSig">The target type sig value.</param>
        /// <param name="context">The context value.</param>
        /// <param name="reverse">Whether to create a reverse proxy when the value is not already a forward proxy.</param>
        private static void EmitDuckChainToProxyConversion(
            ModuleDef moduleDef,
            CilBody body,
            TypeSig proxyTypeSig,
            TypeSig targetTypeSig,
            string context,
            bool reverse = false)
        {
            // Same shapes as dynamic duck typing (MethodIlHelper.AddIlToDuckChain): a value-type target is passed unboxed to
            // CreateCache<TProxy>.CreateFrom<TTarget>, so the proxy is created for the static type (e.g. Nullable<T> itself,
            // not the boxed T), and a null Nullable<T> creates no proxy.
            if (!reverse && targetTypeSig.IsValueType)
            {
                body.Instructions.Add(OpCodes.Call.ToInstruction(CreateDuckTypeCreateCacheCreateFromMethodRef(moduleDef, proxyTypeSig, targetTypeSig)));
                return;
            }

            var importedTargetTypeSig = ImportTypeSigCached(moduleDef, targetTypeSig, $"reference conversion target '{targetTypeSig.FullName}'");
            var targetLocal = new Local(importedTargetTypeSig);
            body.Variables.Add(targetLocal);
            body.InitLocals = true;
            body.Instructions.Add(OpCodes.Stloc.ToInstruction(targetLocal));

            body.Instructions.Add(OpCodes.Ldloc.ToInstruction(targetLocal));
            if (targetTypeSig.IsValueType)
            {
                var importedTargetTypeForBox = ResolveImportedTypeForTypeToken(moduleDef, targetTypeSig, context);
                body.Instructions.Add(OpCodes.Box.ToInstruction(importedTargetTypeForBox));
            }

            if (TryGetNullableElementType(proxyTypeSig, out var nullableProxyElementType))
            {
                var boxedTargetLocal = new Local(moduleDef.CorLibTypes.Object);
                var nullableResultLocal = new Local(ImportTypeSigCached(moduleDef, proxyTypeSig, $"nullable result local '{proxyTypeSig.FullName}'"));
                body.Variables.Add(boxedTargetLocal);
                body.Variables.Add(nullableResultLocal);
                body.InitLocals = true;

                var hasValueLabel = Instruction.Create(OpCodes.Nop);
                var endLabel = Instruction.Create(OpCodes.Nop);
                var importedNullableType = ResolveImportedTypeForTypeToken(moduleDef, proxyTypeSig, context);
                var nullableCtor = CreateNullableCtorRef(moduleDef, proxyTypeSig);

                body.Instructions.Add(OpCodes.Stloc.ToInstruction(boxedTargetLocal));
                body.Instructions.Add(OpCodes.Ldloc.ToInstruction(boxedTargetLocal));
                body.Instructions.Add(OpCodes.Brtrue_S.ToInstruction(hasValueLabel));

                body.Instructions.Add(OpCodes.Ldloca.ToInstruction(nullableResultLocal));
                body.Instructions.Add(OpCodes.Initobj.ToInstruction(importedNullableType));
                body.Instructions.Add(OpCodes.Br_S.ToInstruction(endLabel));

                body.Instructions.Add(hasValueLabel);
                body.Instructions.Add(OpCodes.Ldloc.ToInstruction(boxedTargetLocal));
                var createCacheCreateMethodRef = CreateDuckTypeCreateCacheCreateMethodRef(moduleDef, nullableProxyElementType!, reverse);
                body.Instructions.Add(OpCodes.Call.ToInstruction(createCacheCreateMethodRef));
                body.Instructions.Add(OpCodes.Newobj.ToInstruction(nullableCtor));
                body.Instructions.Add(OpCodes.Stloc.ToInstruction(nullableResultLocal));

                body.Instructions.Add(endLabel);
                body.Instructions.Add(OpCodes.Ldloc.ToInstruction(nullableResultLocal));
                return;
            }

            var createMethodRef = CreateDuckTypeCreateCacheCreateMethodRef(moduleDef, proxyTypeSig, reverse);
            body.Instructions.Add(OpCodes.Call.ToInstruction(createMethodRef));
        }

        /// <summary>
        /// Creates nullable ctor ref.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="nullableTypeSig">The nullable type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethodDefOrRef CreateNullableCtorRef(ModuleDef moduleDef, TypeSig nullableTypeSig)
        {
            if (!TryGetNullableElementType(nullableTypeSig, out _))
            {
                throw new InvalidOperationException($"Expected Nullable<T> type but received '{nullableTypeSig.FullName}'.");
            }

            var cacheKey = BuildTypeSigCacheKey(nullableTypeSig);
            if (_currentExecutionContext?.TryGetNullableCtorRef(cacheKey, out var cachedCtor) == true)
            {
                return cachedCtor!;
            }

            var importedNullableTypeSig = ImportTypeSigCached(moduleDef, nullableTypeSig, $"nullable ctor '{nullableTypeSig.FullName}'");
            var nullableTypeSpec = moduleDef.UpdateRowId(new TypeSpecUser(importedNullableTypeSig));
            var ctorSig = MethodSig.CreateInstance(moduleDef.CorLibTypes.Void, new GenericVar(0));
            var ctorRef = new MemberRefUser(moduleDef, ".ctor", ctorSig, nullableTypeSpec);
            var importedCtor = moduleDef.UpdateRowId(ctorRef);
            _currentExecutionContext?.CacheNullableCtorRef(cacheKey, importedCtor);
            return importedCtor;
        }

        /// <summary>
        /// Gets the instruction that calls a target member (other than a virtual member of a value type, called through a
        /// constrained callvirt) like dynamic duck typing does: callvirt for a public or generic instance method of a reference
        /// type (a NullReferenceException for a null instance, virtual dispatch), and otherwise the exact method, without a null
        /// check, which dynamic duck typing calls through its function pointer (calli). An abstract method, and a member of a
        /// reverse proxy type the registry generates (bound through the method it overrides, see FindMemberDefInHierarchy, on an
        /// instance of exactly that type), are called with callvirt.
        /// </summary>
        /// <param name="moduleDef">The registry module.</param>
        /// <param name="targetType">The target type of the proxy.</param>
        /// <param name="targetMethod">The target method or accessor.</param>
        /// <param name="targetIsValueType">Whether the target type is a value type.</param>
        /// <returns>The call instruction.</returns>
        private static OpCode GetTargetCallOpCode(ModuleDef moduleDef, TypeDef targetType, MethodDef targetMethod, bool targetIsValueType)
        {
            if (targetMethod.IsStatic || targetIsValueType)
            {
                return OpCodes.Call;
            }

            return targetMethod.IsPublic || targetMethod.HasGenericParameters || targetMethod.IsAbstract || targetType.Module == moduleDef
                       ? OpCodes.Callvirt
                       : OpCodes.Call;
        }

        /// <summary>
        /// Gets the method a virtual call to a public override declared by a non-public type of the core library goes through:
        /// the overridden public method of the closest public base type. The runtime's non-public types differ between runtimes
        /// (NativeAOT's don't declare the overrides CoreCLR's do): a virtual call through the public method dispatches to the
        /// same override on both.
        /// </summary>
        /// <param name="targetType">The target type of the proxy.</param>
        /// <param name="targetMethod">The target method or accessor, called with callvirt.</param>
        /// <returns>The method to reference in the call.</returns>
        private static MethodDef GetPortableVirtualCallTarget(TypeDef targetType, MethodDef targetMethod)
        {
            if (!targetMethod.IsVirtual ||
                targetMethod.DeclaringType is not { } declaringType ||
                !IsCoreLibraryAssemblyName(declaringType.Module?.Assembly?.Name?.String) ||
                IsVisibleTypeDefinition(declaringType))
            {
                return targetMethod;
            }

            var slotMethod = targetMethod;
            var visitedMethods = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
            while (visitedMethods.Add(slotMethod) && FindOverriddenBaseMethod(targetType, slotMethod) is { } overriddenMethod)
            {
                if (overriddenMethod.IsPublic && IsVisibleTypeDefinition(overriddenMethod.DeclaringType))
                {
                    return overriddenMethod;
                }

                slotMethod = overriddenMethod;
            }

            return targetMethod;
        }

        /// <summary>
        /// Records why a mapping is specific to the generator's runtime (see DuckTypeAotMappingEmissionResult.RuntimeSpecific):
        /// its target type, or a member its proxy binds, is a type or member of the core library that isn't public.
        /// </summary>
        /// <param name="mapping">The mapping.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="boundMembers">The target members the proxy binds.</param>
        private static void RecordRuntimeSpecificBindings(DuckTypeAotMapping mapping, TypeDef targetType, IEnumerable<IMemberDef?> boundMembers)
        {
            if (_currentExecutionContext is null)
            {
                return;
            }

            string? reason = null;
            if (IsCoreLibraryAssemblyName(targetType.Module?.Assembly?.Name?.String) && !IsVisibleTypeDefinition(targetType))
            {
                reason = $"the target type '{targetType.FullName}'";
            }
            else if (boundMembers.FirstOrDefault(member => member is not null && IsRuntimeSpecificMember(targetType, member)) is { } member)
            {
                reason = $"the member '{member.FullName}'";
            }

            if (reason is not null)
            {
                _currentExecutionContext.RuntimeSpecificMappings[mapping.Key] = reason;
            }
            else
            {
                _currentExecutionContext.RuntimeSpecificMappings.Remove(mapping.Key);
            }
        }

        /// <summary>
        /// Determines whether a bound member is a member of the core library that isn't public (a public override of a non-public
        /// type is called through the public method it overrides, see GetPortableVirtualCallTarget).
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <param name="member">The bound member.</param>
        /// <returns>true if other runtimes may not have the member; otherwise, false.</returns>
        private static bool IsRuntimeSpecificMember(TypeDef targetType, IMemberDef member)
        {
            if (member.DeclaringType is not { } declaringType || !IsCoreLibraryAssemblyName(declaringType.Module?.Assembly?.Name?.String))
            {
                return false;
            }

            return member switch
            {
                MethodDef method => !(method.IsPublic && (IsVisibleTypeDefinition(declaringType) || (method.IsVirtual && GetPortableVirtualCallTarget(targetType, method) != method))),
                FieldDef field => !(field.IsPublic && IsVisibleTypeDefinition(declaringType)),
                _ => false,
            };
        }

        /// <summary>
        /// Applies reverse-target custom attributes to the generated proxy type while preserving dynamic behavior.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="generatedType">The generated type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryApplyReverseTargetCustomAttributes(
            ModuleDef moduleDef,
            TypeDef generatedType,
            TypeDef targetType,
            DuckTypeAotMapping mapping,
            out DuckTypeAotMappingEmissionResult? failure)
        {
            var reverseCustomAttributePlan = GetOrCreateReverseCustomAttributePlan(moduleDef, targetType);
            if (!reverseCustomAttributePlan.Succeeded)
            {
                failure = DuckTypeAotMappingEmissionResult.NotCompatible(
                    mapping,
                    reverseCustomAttributePlan.FailureStatus!,
                    reverseCustomAttributePlan.FailureDiagnosticCode!,
                    reverseCustomAttributePlan.FailureDetail!);
                return false;
            }

            foreach (var clonedAttributePlan in reverseCustomAttributePlan.ClonedAttributes)
            {
                var clonedAttribute = new CustomAttribute(clonedAttributePlan.Constructor);
                foreach (var constructorArgument in clonedAttributePlan.ConstructorArguments)
                {
                    clonedAttribute.ConstructorArguments.Add(constructorArgument);
                }

                generatedType.CustomAttributes.Add(clonedAttribute);
            }

            failure = null;
            return true;
        }

        /// <summary>
        /// Adds add ignores access checks to attributes.
        /// </summary>
        /// <param name="assemblyDef">The assembly def value.</param>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="ignoresAccessChecksToAttributeCtor">The ignores access checks to attribute ctor value.</param>
        /// <param name="mappingResolutionResult">The mapping resolution result value.</param>
        private static void AddIgnoresAccessChecksToAttributes(
            AssemblyDef assemblyDef,
            ModuleDef moduleDef,
            ICustomAttributeType ignoresAccessChecksToAttributeCtor,
            DuckTypeAotMappingResolutionResult mappingResolutionResult,
            IReadOnlyCollection<string> requiredAccessCheckAssemblyNames)
        {
            var assemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                DatadogTraceAssemblyName
            };
            foreach (var assemblyName in mappingResolutionResult.ProxyAssemblyPathsByName.Keys)
            {
                if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName);
                }
            }

            foreach (var assemblyName in mappingResolutionResult.TargetAssemblyPathsByName.Keys)
            {
                if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName);
                }
            }

            foreach (var assemblyName in requiredAccessCheckAssemblyNames)
            {
                if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName);
                }
            }

            // Every other assembly the registry references: a non-public type can also be a generic argument of an imported
            // type (dynamic duck typing makes those visible as well).
            foreach (var typeRef in new MemberFinder().FindAll(moduleDef).TypeRefs.Keys)
            {
                var assemblyName = typeRef.DefinitionAssembly?.Name?.String;
                if (!StringUtil.IsNullOrWhiteSpace(assemblyName))
                {
                    _ = assemblyNames.Add(assemblyName!);
                }
            }

            // The registry itself too: like dynamic duck typing, the forward proxy of a generated reverse proxy can bind its private
            // members (e.g. [DuckField(Name = "_currentInstance")]).
            if (assemblyDef.Name?.String is { Length: > 0 } generatedAssemblyName)
            {
                _ = assemblyNames.Add(generatedAssemblyName);
            }

            foreach (var assemblyName in assemblyNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                var customAttribute = new CustomAttribute(ignoresAccessChecksToAttributeCtor);
                customAttribute.ConstructorArguments.Add(new CAArgument(moduleDef.CorLibTypes.String, assemblyName));
                assemblyDef.CustomAttributes.Add(customAttribute);
            }
        }
    }
}
