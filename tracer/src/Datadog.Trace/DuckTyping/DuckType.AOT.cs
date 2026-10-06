// <copyright file="DuckType.AOT.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// AOT-specific DuckType APIs and runtime-mode state.
    /// </summary>
    public static partial class DuckType
    {
        private const string ManualRegistrationObsoleteMessage = "Reserved for generated NativeAOT registry bootstrap. Manual registration and registry mixing are unsupported.";
        private const int RuntimeModeStateUninitialized = 0;

        // An assembly-qualified type name is at most 1024 characters: longer proxy type names are truncated (see CreateTypeAndModuleBuilder).
        private const int MaxProxyTypeNameLength = 1023;

        private static int _runtimeModeState;

        internal static DuckTypeRuntimeMode RuntimeMode => Volatile.Read(ref _runtimeModeState) == (int)DuckTypeRuntimeMode.Aot
                                                               ? DuckTypeRuntimeMode.Aot
                                                               : DuckTypeRuntimeMode.Dynamic;

        /// <summary>
        /// Enables NativeAOT runtime mode.
        /// </summary>
        public static void EnableAotMode()
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
        }

        /// <summary>
        /// Registers a forward AOT proxy.
        /// </summary>
        /// <param name="proxyDefinitionType">Duck typing proxy definition type.</param>
        /// <param name="targetType">Runtime target type.</param>
        /// <param name="generatedProxyType">Generated proxy implementation type.</param>
        /// <param name="activator">Activator used to create proxy instances.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, Func<object?, object?> activator)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterProxy(proxyDefinitionType, targetType, generatedProxyType, activator);
        }

        /// <summary>
        /// Registers a forward AOT proxy using an object-bridge method handle.
        /// </summary>
        /// <param name="proxyDefinitionType">Duck typing proxy definition type.</param>
        /// <param name="targetType">Runtime target type.</param>
        /// <param name="generatedProxyType">Generated proxy implementation type.</param>
        /// <param name="activatorMethodHandle">Static activator method handle. The method must accept object and return a proxy-compatible value.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotProxy(Type proxyDefinitionType, Type targetType, Type generatedProxyType, RuntimeMethodHandle activatorMethodHandle)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterProxy(proxyDefinitionType, targetType, generatedProxyType, activatorMethodHandle);
        }

        /// <summary>
        /// Registers a reverse AOT proxy.
        /// </summary>
        /// <param name="typeToDeriveFrom">Type to derive the reverse proxy from.</param>
        /// <param name="delegationType">Type that provides delegated implementations.</param>
        /// <param name="generatedProxyType">Generated reverse proxy implementation type.</param>
        /// <param name="activator">Activator used to create reverse proxy instances.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotReverseProxy(Type typeToDeriveFrom, Type delegationType, Type generatedProxyType, Func<object?, object?> activator)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterReverseProxy(typeToDeriveFrom, delegationType, generatedProxyType, activator);
        }

        /// <summary>
        /// Registers a reverse AOT proxy using an object-bridge method handle.
        /// </summary>
        /// <param name="typeToDeriveFrom">Type to derive the reverse proxy from.</param>
        /// <param name="delegationType">Type that provides delegated implementations.</param>
        /// <param name="generatedProxyType">Generated reverse proxy implementation type.</param>
        /// <param name="activatorMethodHandle">Static reverse activator method handle. The method must accept object and return a proxy-compatible value.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotReverseProxy(Type typeToDeriveFrom, Type delegationType, Type generatedProxyType, RuntimeMethodHandle activatorMethodHandle)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterReverseProxy(typeToDeriveFrom, delegationType, generatedProxyType, activatorMethodHandle);
        }

        /// <summary>
        /// Registers a forward AOT failure using a nonthrowing exception factory.
        /// </summary>
        /// <param name="proxyDefinitionType">Duck typing proxy definition type.</param>
        /// <param name="targetType">Runtime target type.</param>
        /// <param name="exceptionFactory">Static factory used once to create the cached failure exception.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotProxyFailureFactory(Type proxyDefinitionType, Type targetType, Func<Exception> exceptionFactory)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterProxyFailureFactory(proxyDefinitionType, targetType, exceptionFactory);
        }

        /// <summary>
        /// Registers a forward mapping failure in AOT mode.
        /// </summary>
        /// <param name="proxyDefinitionType">Duck typing proxy definition type.</param>
        /// <param name="targetType">Runtime target type.</param>
        /// <param name="exceptionType">Exception type to throw for this mapping.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotProxyFailure(Type proxyDefinitionType, Type targetType, Type exceptionType)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterProxyFailure(proxyDefinitionType, targetType, exceptionType);
        }

        /// <summary>
        /// Registers a forward mapping failure in AOT mode using a generated thrower.
        /// </summary>
        /// <param name="proxyDefinitionType">Duck typing proxy definition type.</param>
        /// <param name="targetType">Runtime target type.</param>
        /// <param name="failureThrower">Static failure thrower delegate.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotProxyFailure(Type proxyDefinitionType, Type targetType, Action failureThrower)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterProxyFailure(proxyDefinitionType, targetType, failureThrower);
        }

        /// <summary>
        /// Registers a forward mapping failure in AOT mode using a generated thrower.
        /// </summary>
        /// <param name="proxyDefinitionType">Duck typing proxy definition type.</param>
        /// <param name="targetType">Runtime target type.</param>
        /// <param name="throwerMethodHandle">Static failure thrower method handle.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotProxyFailure(Type proxyDefinitionType, Type targetType, RuntimeMethodHandle throwerMethodHandle)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterProxyFailure(proxyDefinitionType, targetType, throwerMethodHandle);
        }

        /// <summary>
        /// Registers a reverse AOT failure using a nonthrowing exception factory.
        /// </summary>
        /// <param name="typeToDeriveFrom">Type to derive the reverse proxy from.</param>
        /// <param name="delegationType">Type that provides delegated implementations.</param>
        /// <param name="exceptionFactory">Static factory used once to create the cached failure exception.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotReverseProxyFailureFactory(Type typeToDeriveFrom, Type delegationType, Func<Exception> exceptionFactory)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterReverseProxyFailureFactory(typeToDeriveFrom, delegationType, exceptionFactory);
        }

        /// <summary>
        /// Registers a reverse mapping failure in AOT mode.
        /// </summary>
        /// <param name="typeToDeriveFrom">Type to derive the reverse proxy from.</param>
        /// <param name="delegationType">Type that provides delegated implementations.</param>
        /// <param name="exceptionType">Exception type to throw for this mapping.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotReverseProxyFailure(Type typeToDeriveFrom, Type delegationType, Type exceptionType)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterReverseProxyFailure(typeToDeriveFrom, delegationType, exceptionType);
        }

        /// <summary>
        /// Registers a reverse mapping failure in AOT mode using a generated thrower.
        /// </summary>
        /// <param name="typeToDeriveFrom">Type to derive the reverse proxy from.</param>
        /// <param name="delegationType">Type that provides delegated implementations.</param>
        /// <param name="failureThrower">Static reverse failure thrower delegate.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotReverseProxyFailure(Type typeToDeriveFrom, Type delegationType, Action failureThrower)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterReverseProxyFailure(typeToDeriveFrom, delegationType, failureThrower);
        }

        /// <summary>
        /// Registers a reverse mapping failure in AOT mode using a generated thrower.
        /// </summary>
        /// <param name="typeToDeriveFrom">Type to derive the reverse proxy from.</param>
        /// <param name="delegationType">Type that provides delegated implementations.</param>
        /// <param name="throwerMethodHandle">Static reverse failure thrower method handle.</param>
        [Obsolete(ManualRegistrationObsoleteMessage, error: false)]
        public static void RegisterAotReverseProxyFailure(Type typeToDeriveFrom, Type delegationType, RuntimeMethodHandle throwerMethodHandle)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.RegisterReverseProxyFailure(typeToDeriveFrom, delegationType, throwerMethodHandle);
        }

        /// <summary>
        /// Validates an AOT-generated registry contract.
        /// </summary>
        /// <param name="schemaVersion">AOT contract schema version.</param>
        /// <param name="datadogTraceAssemblyVersion">Datadog.Trace assembly version used by the generator.</param>
        /// <param name="datadogTraceAssemblyMvid">Datadog.Trace assembly MVID used by the generator.</param>
        /// <param name="registryAssemblyFullName">Generated registry assembly full name.</param>
        /// <param name="registryAssemblyMvid">Generated registry assembly module MVID.</param>
        public static void ValidateAotRegistryContract(
            string schemaVersion,
            string datadogTraceAssemblyVersion,
            string datadogTraceAssemblyMvid,
            string registryAssemblyFullName,
            string registryAssemblyMvid)
        {
            EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode.Aot);
            DuckTypeAotEngine.ValidateContract(
                new DuckTypeAotContract(schemaVersion, datadogTraceAssemblyVersion, datadogTraceAssemblyMvid),
                new DuckTypeAotAssemblyMetadata(registryAssemblyFullName, registryAssemblyMvid));
        }

        /// <summary>
        /// Selects the target method dynamic duck typing binds a forward proxy method to. The AOT registry generator uses it
        /// to bind exactly the same method, instead of re-implementing the selection rules (Type.GetMethod lookups,
        /// ExplicitInterfaceTypeName, ParameterTypeNames, overload resolution...).
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <param name="proxyMethod">The forward proxy method definition.</param>
        /// <returns>The selected target method, or null when dynamic duck typing can't select one (proxy creation then fails).</returns>
        internal static MethodInfo? SelectForwardTargetMethodForAot(Type targetType, MethodInfo proxyMethod)
        {
            try
            {
                // Same arguments as CreateMethods passes for every forward proxy method.
                var proxyMethodParameters = proxyMethod.GetParameters();
                var proxyMethodParametersTypes = proxyMethodParameters.Select(p => p.ParameterType).ToArray();
                var allTargetMethods = targetType.GetMethods(DuckAttribute.DefaultFlags);
                return SelectTargetMethod<DuckAttribute>(targetType, proxyMethod, proxyMethodParameters, proxyMethodParametersTypes, allTargetMethods, out var targetMethod) is null
                           ? targetMethod
                           : null;
            }
            catch (Exception)
            {
                // E.g. an ambiguous Type.GetMethod lookup: dynamic proxy creation fails as well.
                return null;
            }
        }

        /// <summary>
        /// Selects the target member (property or field) dynamic duck typing binds a forward proxy property to, so the AOT
        /// registry generator binds the same member (ExplicitInterfaceTypeName, FallbackToBaseTypes, hidden members...).
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="proxyAccessor">An accessor of the proxy property.</param>
        /// <returns>The selected <see cref="PropertyInfo"/> or <see cref="FieldInfo"/>, or null when dynamic duck typing selects none.</returns>
        internal static MemberInfo? SelectForwardTargetPropertyOrFieldForAot(Type proxyDefinitionType, Type targetType, MethodInfo proxyAccessor)
        {
            try
            {
                // The properties CreateProperties implements, so the [Duck] attribute is read from the same PropertyInfo. It
                // implements one property per name: the one implementing a property it skipped (another interface's property
                // with the same name, like in a diamond) implements its accessors too.
                var proxyProperties = GetProperties(proxyDefinitionType);
                var proxyProperty = proxyProperties.FirstOrDefault(property => IsSameMethodSlot(property.GetMethod, proxyAccessor) || IsSameMethodSlot(property.SetMethod, proxyAccessor));
                if (proxyProperty is null &&
                    proxyAccessor.DeclaringType?.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                                 .FirstOrDefault(property => property.GetMethod == proxyAccessor || property.SetMethod == proxyAccessor) is { } accessorProperty)
                {
                    proxyProperty = proxyProperties.FirstOrDefault(property => property.Name == accessorProperty.Name);
                }

                if (proxyProperty is null)
                {
                    return null;
                }

                // Same selection as CreateProperties.
                var duckAttribute = proxyProperty.GetCustomAttribute<DuckAttribute>(true) ?? new DuckAttribute();
                var name = duckAttribute.Name ?? proxyProperty.Name;
                return duckAttribute.Kind switch
                {
                    DuckKind.Property => FindForwardTargetProperty(targetType, duckAttribute, name, proxyProperty),
                    DuckKind.PropertyOrField => (MemberInfo?)FindForwardTargetProperty(targetType, duckAttribute, name, proxyProperty) ?? FindTargetField(targetType, duckAttribute, name),
                    DuckKind.Field => FindTargetField(targetType, duckAttribute, name),
                    _ => null,
                };
            }
            catch (Exception)
            {
                // E.g. an ambiguous Type.GetProperty lookup: dynamic proxy creation fails as well.
                return null;
            }

            static bool IsSameMethodSlot(MethodInfo? method, MethodInfo other)
            {
                if (method is null)
                {
                    return false;
                }

                var methodDefinition = method.GetBaseDefinition();
                var otherDefinition = other.GetBaseDefinition();
                return methodDefinition.MetadataToken == otherDefinition.MetadataToken && methodDefinition.Module == otherDefinition.Module;
            }
        }

        /// <summary>
        /// Selects the target member (property or field) dynamic duck typing copies into a [DuckCopy] struct field.
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <param name="proxyField">The [DuckCopy] struct field.</param>
        /// <returns>The selected <see cref="PropertyInfo"/> or <see cref="FieldInfo"/>, or null when dynamic duck typing selects none.</returns>
        internal static MemberInfo? SelectDuckCopyTargetMemberForAot(Type targetType, FieldInfo proxyField)
        {
            try
            {
                // Same selection as CreatePropertiesFromStruct.
                var duckAttribute = proxyField.GetCustomAttribute<DuckAttribute>(true) ?? new DuckAttribute();
                var name = duckAttribute.Name ?? proxyField.Name;
                if (duckAttribute.Kind is DuckKind.Property or DuckKind.PropertyOrField)
                {
                    if (FindDuckCopyTargetProperty(targetType, duckAttribute, name, out var targetProperty) is not null)
                    {
                        return null;
                    }

                    if (targetProperty is not null || duckAttribute.Kind == DuckKind.Property)
                    {
                        return targetProperty;
                    }
                }

                return FindTargetField(targetType, duckAttribute, name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Selects the methods a dynamic reverse proxy implements, like CreateReverseProxyMethods: each [DuckReverseMethod]
        /// method of the delegation type, in declaration order, implements the overridable method it selects.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type the reverse proxy derives from.</param>
        /// <param name="typeToDelegateTo">The delegation type.</param>
        /// <returns>The overridden methods with their implementation, or null when dynamic duck typing can't select them.</returns>
        internal static IReadOnlyList<KeyValuePair<MethodInfo, MethodInfo>>? SelectReverseImplementationMethodsForAot(Type typeToDeriveFrom, Type typeToDelegateTo)
        {
            try
            {
                var overriddenMethods = GetMethods(typeToDeriveFrom);
                var selectedMethods = new List<KeyValuePair<MethodInfo, MethodInfo>>();
                foreach (var implementationMethod in typeToDelegateTo.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (implementationMethod.GetCustomAttribute<DuckReverseMethodAttribute>(true) is null)
                    {
                        continue;
                    }

                    var implementationMethodParameters = implementationMethod.GetParameters();
                    if (SelectTargetMethod<DuckReverseMethodAttribute>(typeToDeriveFrom, implementationMethod, implementationMethodParameters, implementationMethodParameters.Select(p => p.ParameterType).ToArray(), overriddenMethods, out var overriddenMethod) is not null ||
                        overriddenMethod is null)
                    {
                        return null;
                    }

                    overriddenMethods.Remove(overriddenMethod);
                    selectedMethods.Add(new KeyValuePair<MethodInfo, MethodInfo>(overriddenMethod, implementationMethod));
                }

                return selectedMethods;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the [DuckReverseMethod] property accessors of the delegation type dynamic duck typing implements the property
        /// accessors of a reverse proxy with (see CreateReverseProxyProperties): its public properties, in declaration order, each
        /// bound to the property with its name. When two of them implement the same property, the runtime uses the first method
        /// implementing an interface method, and the last override of a class method. Used by the AOT registry generator.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type the reverse proxy derives from.</param>
        /// <param name="typeToDelegateTo">The delegation type.</param>
        /// <returns>The overridden accessors with their implementations, or null if dynamic duck typing fails to select them.</returns>
        internal static IReadOnlyList<KeyValuePair<MethodInfo, MethodInfo>>? SelectReverseImplementationAccessorsForAot(Type typeToDeriveFrom, Type typeToDelegateTo)
        {
            try
            {
                var selectedAccessors = new List<KeyValuePair<MethodInfo, MethodInfo>>();
                foreach (var implementationProperty in typeToDelegateTo.GetProperties())
                {
                    if (implementationProperty.GetCustomAttribute<DuckReverseMethodAttribute>(true) is not { } duckAttribute)
                    {
                        continue;
                    }

                    if (GetTargetPropertyOrIndex(typeToDeriveFrom, duckAttribute.Name ?? implementationProperty.Name, duckAttribute.BindingFlags, implementationProperty, duckAttribute.ExplicitInterfaceTypeName) is not { } overriddenProperty)
                    {
                        return null;
                    }

                    if (implementationProperty.CanRead && overriddenProperty.GetMethod is { } overriddenGetter && implementationProperty.GetMethod is { } implementationGetter)
                    {
                        Select(overriddenGetter, implementationGetter);
                    }

                    if (implementationProperty.CanWrite && overriddenProperty.SetMethod is { } overriddenSetter && implementationProperty.SetMethod is { } implementationSetter)
                    {
                        Select(overriddenSetter, implementationSetter);
                    }
                }

                return selectedAccessors;

                void Select(MethodInfo overriddenAccessor, MethodInfo implementationAccessor)
                {
                    var selectedIndex = selectedAccessors.FindIndex(selected => selected.Key == overriddenAccessor);
                    if (selectedIndex < 0)
                    {
                        selectedAccessors.Add(new KeyValuePair<MethodInfo, MethodInfo>(overriddenAccessor, implementationAccessor));
                    }
                    else if (!typeToDeriveFrom.IsInterface)
                    {
                        selectedAccessors[selectedIndex] = new KeyValuePair<MethodInfo, MethodInfo>(overriddenAccessor, implementationAccessor);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Determines whether dynamic duck typing duck chains a value of <paramref name="targetType"/> into
        /// <paramref name="proxyType"/>, instead of converting it.
        /// </summary>
        /// <param name="targetType">The target member type.</param>
        /// <param name="proxyType">The proxy member type.</param>
        /// <returns>true if dynamic duck typing creates a duck chaining proxy; otherwise, false.</returns>
        internal static bool NeedsDuckChainingForAot(Type targetType, Type proxyType) => NeedsDuckChaining(targetType, proxyType);

        /// <summary>
        /// Gets the exception dynamic duck typing throws when it creates the proxy type of a pair, like when a proxy is first
        /// requested. Used by the AOT registry generator, which replays that failure and binds nothing dynamic duck typing can't
        /// create. A dry run binds the members without creating the type: it misses the shapes Reflection.Emit can't create (a
        /// sealed base type, an abstract member left without implementation, an event...).
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type, or the type to derive from for a reverse proxy.</param>
        /// <param name="targetType">The target type, or the delegation type for a reverse proxy.</param>
        /// <param name="reverse">Whether the proxy is a reverse proxy.</param>
        /// <param name="dryRun">Whether to only bind the members, without creating the type.</param>
        /// <returns>The exception dynamic duck typing throws, or null if it creates the proxy type.</returns>
        internal static Exception? GetDynamicProxyTypeFailureForAot(Type proxyDefinitionType, Type targetType, bool reverse, bool dryRun)
        {
            try
            {
                var result = reverse
                                 ? CreateReverseProxyType(proxyDefinitionType, targetType, dryRun)
                                 : CreateProxyType(proxyDefinitionType, targetType, dryRun);
                return result.FailureException;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Gets what the type dynamic duck typing creates for a forward proxy takes from its target, besides the members of the
        /// proxy definition: the attributes of the target's ToString it copies, the target's [DuckInclude] methods it implements
        /// and, for value types, the type of the instance field. Targets with the same key get a proxy type of the same shape, so
        /// the AOT registry generator creates it once to know whether Reflection.Emit can create it for all of them.
        /// </summary>
        /// <param name="targetType">The target type.</param>
        /// <returns>The shape key.</returns>
        internal static object GetForwardProxyShapeKeyForAot(Type targetType)
        {
            try
            {
                if (targetType.IsValueType || GetMethods(targetType).Any(method => method.GetCustomAttribute<DuckIncludeAttribute>(true) is not null))
                {
                    return targetType;
                }

                return targetType.GetMethod(nameof(IDuckType.ToString), Type.EmptyTypes)?.Attributes ?? (object)string.Empty;
            }
            catch (Exception)
            {
                // Whatever fails here fails when the type is created too: don't share the creation.
                return targetType;
            }
        }

        /// <summary>
        /// Gets the reverse proxy type dynamic duck typing creates for a pair. Used by the AOT registry generator to know what
        /// dynamic duck typing does with a reverse proxy instance, e.g. when it's duck typed again.
        /// </summary>
        /// <param name="typeToDeriveFrom">The type the reverse proxy derives from or implements.</param>
        /// <param name="typeToDelegateTo">The delegation type.</param>
        /// <returns>The reverse proxy type, or null if dynamic duck typing can't create it.</returns>
        internal static Type? GetDynamicReverseProxyTypeForAot(Type typeToDeriveFrom, Type typeToDelegateTo)
        {
            try
            {
                var result = CreateReverseProxyType(typeToDeriveFrom, typeToDelegateTo, dryRun: false);
                return result.CanCreate() ? result.ProxyType : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Determines whether a type load failure of dynamic duck typing is about the proxy type it creates for a pair, not
        /// about a type the proxy uses. The AOT registry generator replays the former only: the latter comes from assemblies its
        /// process can't load. Proxy types are named after the pair, then a counter (see CreateTypeAndModuleBuilder).
        /// </summary>
        /// <param name="exception">The type load failure.</param>
        /// <param name="proxyDefinitionType">The proxy definition type, or the type to derive from for a reverse proxy.</param>
        /// <param name="targetType">The target type, or the delegation type for a reverse proxy.</param>
        /// <returns>true if the failure is about the proxy type; otherwise, false.</returns>
        internal static bool IsDynamicProxyTypeLoadFailureForAot(TypeLoadException exception, Type proxyDefinitionType, Type targetType)
        {
            var typeName = exception.TypeName;
            if (StringUtil.IsNullOrEmpty(typeName))
            {
                // No type name: the message still names the dynamic assembly (message arguments aren't localized).
                return exception.Message.IndexOf("Datadog.DuckType", StringComparison.Ordinal) >= 0;
            }

            var counterSeparator = typeName.LastIndexOf('_');
            if (counterSeparator < 0 || counterSeparator == typeName.Length - 1)
            {
                return false;
            }

            for (var i = counterSeparator + 1; i < typeName.Length; i++)
            {
                if (typeName[i] is < '0' or > '9')
                {
                    return false;
                }
            }

            // The name of the pair, or its beginning when the name is truncated to the maximum length.
            var prefix = GetProxyTypeNamePrefix(proxyDefinitionType, targetType);
            return (counterSeparator == prefix.Length || (typeName.Length == MaxProxyTypeNameLength && counterSeparator < prefix.Length)) &&
                   string.CompareOrdinal(typeName, 0, prefix, 0, counterSeparator) == 0;
        }

        /// <summary>
        /// Test-only reset for DuckType runtime mode and shared caches.
        /// </summary>
        internal static void ResetRuntimeModeForTests()
        {
            DuckTypeAotEngine.ResetForTests();
            DuckTypeCache.Clear();
            DuckTypeReverseCache.Clear();
            lock (Locker)
            {
                ActiveBuilders.Clear();
                IgnoresAccessChecksToAssembliesSetDictionary.Clear();
                _assemblyCount = 0;
                _typeCount = 0;
            }

            Volatile.Write(ref _runtimeModeState, RuntimeModeStateUninitialized);

            // Invalidate after every cache and the runtime mode have been reset, so no fast path entry computed
            // against the previous state is kept.
            InvalidateFastPaths();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DuckTypeRuntimeMode EnsureRuntimeModeIsInitialized()
        {
            var currentState = Volatile.Read(ref _runtimeModeState);
            if (currentState == RuntimeModeStateUninitialized)
            {
                currentState = Interlocked.CompareExchange(ref _runtimeModeState, (int)DuckTypeRuntimeMode.Dynamic, RuntimeModeStateUninitialized);
                if (currentState == RuntimeModeStateUninitialized)
                {
                    currentState = (int)DuckTypeRuntimeMode.Dynamic;
                }
            }

            return currentState == (int)DuckTypeRuntimeMode.Aot ? DuckTypeRuntimeMode.Aot : DuckTypeRuntimeMode.Dynamic;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureRuntimeModeIsInitialized(DuckTypeRuntimeMode mode)
        {
            var requestedState = (int)mode;
            var currentState = Interlocked.CompareExchange(ref _runtimeModeState, requestedState, RuntimeModeStateUninitialized);
            if (currentState == RuntimeModeStateUninitialized)
            {
                return;
            }

            var currentMode = currentState == (int)DuckTypeRuntimeMode.Aot ? DuckTypeRuntimeMode.Aot : DuckTypeRuntimeMode.Dynamic;
            if (currentMode != mode)
            {
                DuckTypeRuntimeModeConflictException.Throw(currentMode, mode);
            }
        }

        private static CreateTypeResult GetOrCreateDynamicProxyType(Type proxyType, Type targetType)
        {
            return DuckTypeCache.GetOrAdd(
                new TypesTuple(proxyType, targetType),
                key => new Lazy<CreateTypeResult>(() =>
                {
                    DuckTypeAotDiscoveryRecorder.Record(key.ProxyDefinitionType, key.TargetType, reverse: false);
                    var dryResult = CreateProxyType(key.ProxyDefinitionType, key.TargetType, true);
                    if (dryResult.CanCreate())
                    {
                        return CreateProxyType(key.ProxyDefinitionType, key.TargetType, false);
                    }

                    return dryResult;
                }))
                .Value;
        }

        private static CreateTypeResult GetOrCreateDynamicReverseProxyType(Type typeToDeriveFrom, Type delegationType)
        {
            return DuckTypeReverseCache.GetOrAdd(
                new TypesTuple(typeToDeriveFrom, delegationType),
                key => new Lazy<CreateTypeResult>(() =>
                {
                    DuckTypeAotDiscoveryRecorder.Record(key.ProxyDefinitionType, key.TargetType, reverse: true);
                    var dryResult = CreateReverseProxyType(key.ProxyDefinitionType, key.TargetType, true);
                    if (dryResult.CanCreate())
                    {
                        return CreateReverseProxyType(key.ProxyDefinitionType, key.TargetType, false);
                    }

                    return dryResult;
                }))
                .Value;
        }
    }
}
