// <copyright file="DuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Create struct proxy instance delegate
    /// </summary>
    /// <typeparam name="T">Type of struct</typeparam>
    /// <param name="instance">Object instance</param>
    /// <returns>Proxy instance</returns>
    [return: NotNull]
    internal delegate T CreateProxyInstance<T>(object? instance);

    /// <summary>
    /// Duck Type
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static partial class DuckType
    {
        /// <summary>
        /// Create duck type proxy using a base type
        /// </summary>
        /// <param name="instance">Instance object</param>
        /// <typeparam name="T">Duck type</typeparam>
        /// <returns>Duck type proxy</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNullIfNotNull("instance")]
        public static T? Create<T>(object? instance)
        {
            return CreateCache<T>.Create(instance);
        }

        /// <summary>
        /// Create duck type proxy using a base type
        /// </summary>
        /// <param name="proxyType">Duck type</param>
        /// <param name="instance">Instance object</param>
        /// <returns>Duck Type proxy</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object Create(Type proxyType, object instance)
        {
            // Validate arguments
            EnsureArguments(proxyType, instance);

            // Create Type
            CreateTypeResult result = GetOrCreateProxyType(proxyType, instance.GetType());

            // Create instance
            return result.CreateInstance(instance);
        }

        /// <summary>
        /// Gets if a proxy can be created
        /// </summary>
        /// <param name="instance">Instance object</param>
        /// <typeparam name="T">Duck type</typeparam>
        /// <returns>true if the proxy can be created; otherwise, false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanCreate<T>(object? instance)
        {
            return CreateCache<T>.CanCreate(instance);
        }

        /// <summary>
        /// Gets if a proxy can be created
        /// </summary>
        /// <param name="proxyType">Duck type</param>
        /// <param name="instance">Instance object</param>
        /// <returns>true if the proxy can be created; otherwise, false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanCreate(Type proxyType, object instance)
        {
            // Validate arguments
            EnsureArguments(proxyType, instance);

            // Create Type
            CreateTypeResult result = GetOrCreateProxyType(proxyType, instance.GetType());

            // Create instance
            return result.CanCreate();
        }

        /// <summary>
        /// Gets or create a new proxy type for ducktyping
        /// </summary>
        /// <param name="proxyType">ProxyType interface</param>
        /// <param name="targetType">Target type</param>
        /// <returns>CreateTypeResult instance</returns>
        public static CreateTypeResult GetOrCreateProxyType(Type proxyType, Type targetType)
        {
            return GetOrCreateProxyType(proxyType, targetType, reverse: false);
        }

        /// <summary>
        /// Create duck type proxy using a base type
        /// </summary>
        /// <param name="typeToDeriveFrom">The type to derive from</param>
        /// <param name="delegationInstance">The instance to which additional implementation details are delegated</param>
        /// <returns>Duck Type proxy</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object CreateReverse(Type typeToDeriveFrom, object delegationInstance)
        {
            // Validate arguments
            EnsureArguments(typeToDeriveFrom, delegationInstance);

            if (TryUnwrapForwardProxy(delegationInstance, typeToDeriveFrom, out var original))
            {
                return original;
            }

            // Create Type
            CreateTypeResult result = GetOrCreateReverseProxyType(typeToDeriveFrom, delegationInstance.GetType());

            // Create instance
            return result.CreateInstance(delegationInstance);
        }

        /// <summary>
        /// Gets or create a new reverse proxy type for ducktyping
        /// </summary>
        /// <param name="typeToDeriveFrom">The type to derive from</param>
        /// <param name="delegationType">The type to delegate additional implementations to</param>
        /// <returns>CreateTypeResult instance</returns>
        public static CreateTypeResult GetOrCreateReverseProxyType(Type typeToDeriveFrom, Type delegationType)
        {
            return GetOrCreateProxyType(typeToDeriveFrom, delegationType, reverse: true);
        }

        // Not inlined: the callers' slow paths (e.g. CreateCache<T>.GetProxy, inlined in every DuckType.Create<T> call site)
        // stay small.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static CreateTypeResult GetOrCreateProxyType(Type proxyType, Type targetType, bool reverse)
        {
            // The engines own their caches; per proxy definition fast paths live in CreateCache<T>.
            if (EnsureRuntimeModeIsInitialized() == DuckTypeRuntimeMode.Aot)
            {
                return reverse ? DuckTypeAotEngine.GetOrCreateReverseProxyType(proxyType, targetType) : DuckTypeAotEngine.GetOrCreateProxyType(proxyType, targetType);
            }

            return reverse ? GetOrCreateDynamicReverseProxyType(proxyType, targetType) : GetOrCreateDynamicProxyType(proxyType, targetType);
        }

        /// <summary>
        /// Gets the original instance of a forward proxy when it already derives from <paramref name="typeToDeriveFrom"/>,
        /// so creating a reverse proxy over a forward proxy round-trips to the original instance instead of wrapping it.
        /// <see cref="CreateCache{T}.CreateReverse"/> applies the same rule through its generic type check.
        /// </summary>
        /// <param name="instance">The instance a reverse proxy is requested for.</param>
        /// <param name="typeToDeriveFrom">The type the reverse proxy derives from.</param>
        /// <param name="original">The original instance wrapped by the forward proxy.</param>
        /// <returns>true if <paramref name="instance"/> is a forward proxy over an instance of <paramref name="typeToDeriveFrom"/>; otherwise, false.</returns>
        internal static bool TryUnwrapForwardProxy(object? instance, Type typeToDeriveFrom, [NotNullWhen(true)] out object? original)
        {
            if (instance is IDuckType { Instance: { } wrapped } && typeToDeriveFrom.IsInstanceOfType(wrapped))
            {
                original = wrapped;
                return true;
            }

            original = null;
            return false;
        }

        /// <summary>
        /// Invalidates every <see cref="CreateCache{T}"/> fast path entry. Must be called after the state that produced
        /// cached <see cref="CreateTypeResult"/> values changes (AOT registrations, test resets).
        /// </summary>
        internal static void InvalidateFastPaths()
        {
            // Under the lock entries are stored with (see StoreFastPath): an entry computed before this invalidation is either
            // cleared here or never stored, so a lookup that starts after it can't see a stale entry.
            lock (FastPathResets)
            {
                Interlocked.Increment(ref _fastPathVersion);
                foreach (var fastPathReset in FastPathResets)
                {
                    fastPathReset();
                }
            }
        }

        private static void StoreFastPath(ref FastPathEntry? fastPathSlot, ref bool resetRegistered, Action fastPathReset, in CreateTypeResult result, int version)
        {
            lock (FastPathResets)
            {
                if (!resetRegistered)
                {
                    FastPathResets.Add(fastPathReset);
                    resetRegistered = true;
                }

                // A result computed while an invalidation happened may be stale, so it isn't stored.
                if (fastPathSlot is null && _fastPathVersion == version)
                {
                    Volatile.Write(ref fastPathSlot, new FastPathEntry(result));
                }
            }
        }

        private static CreateTypeResult CreateProxyType(Type proxyDefinitionType, Type targetType, bool dryRun)
        {
            // When doing normal duck typing, we create a type that derives from proxyDefinitionType (MyImplementation)
            // and overrides methods to call targetType (Original) (which is stored in an instance field) e.g.

            // public class Proxy: MyImplementation, IDuckType
            // {
            //     public object Instance {get;set;} // Original
            //     public bool SomeDelegatedMethod() => Instance.SomeDelegatedMethod();
            //     public IDuck SomeOtherWithParams(IDuckParam2 duck)
            //     {
            //         OrigParam orig = duck.Instance;
            //         OrigResult result = Instance.SomeOtherWithParams(orig)
            //         return DuckType.CreateCache<IDuck>.Create(result);
            //     }
            // }

            lock (Locker)
            {
                try
                {
                    ModuleBuilder? moduleBuilder = null;
                    TypeBuilder? proxyTypeBuilder = null;
                    FieldInfo? instanceField = null;

                    if (!dryRun)
                    {
                        moduleBuilder = CreateTypeAndModuleBuilder(proxyDefinitionType, targetType, out proxyTypeBuilder, out instanceField);
                    }

                    if (proxyDefinitionType.IsValueType)
                    {
                        // Create Fields and Properties from the struct information
                        if (CreatePropertiesFromStruct(proxyTypeBuilder, proxyDefinitionType, targetType, instanceField) is { } structError)
                        {
                            return Failed(structError);
                        }

                        if (dryRun)
                        {
                            // Dry run
                            return new CreateTypeResult(proxyDefinitionType, null, targetType, null, exceptionInfo: null);
                        }

                        // Create Type
                        Type proxyType = proxyTypeBuilder!.CreateTypeInfo()!.AsType();

                        if (CreateStructCopyMethod(moduleBuilder, proxyDefinitionType, proxyType, targetType, out var structActivator) is { } copyError)
                        {
                            return Failed(copyError);
                        }

                        return new CreateTypeResult(proxyDefinitionType, proxyType, targetType, structActivator, exceptionInfo: null);
                    }
                    else
                    {
                        // Create Fields and Properties
                        if (CreateProperties(proxyTypeBuilder, proxyDefinitionType, targetType, instanceField) is { } propertyError)
                        {
                            return Failed(propertyError);
                        }

                        // Create Methods
                        if (CreateMethods(proxyTypeBuilder, proxyDefinitionType, targetType, instanceField) is { } methodError)
                        {
                            return Failed(methodError);
                        }

                        if (dryRun)
                        {
                            // Dry run
                            return new CreateTypeResult(proxyDefinitionType, null, targetType, null, exceptionInfo: null);
                        }

                        // Create Type
                        Type proxyType = proxyTypeBuilder!.CreateTypeInfo()!.AsType();
                        return new CreateTypeResult(proxyDefinitionType, proxyType, targetType, GetCreateProxyInstanceDelegate(moduleBuilder, proxyDefinitionType, proxyType, targetType), exceptionInfo: null);
                    }
                }
                catch (DuckTypeException ex)
                {
                    return Failed(ex);
                }
                catch (Exception ex)
                {
                    // An unexpected fault from Reflection.Emit or reflection. Construct the wrapper instead of
                    // throwing and catching our own exception, so this path costs one first-chance exception
                    // rather than two.
                    return Failed(DuckTypeException.Create($"Error creating duck type for type: '{targetType}' using proxy: '{proxyDefinitionType}'", ex));
                }

                CreateTypeResult Failed(DuckTypeException error)
                    => new(proxyDefinitionType, proxyType: null, targetType, activator: null, ExceptionDispatchInfo.Capture(error));
            }
        }

        private static CreateTypeResult CreateReverseProxyType(Type typeToDeriveFrom, Type typeToDelegateTo, bool dryRun)
        {
            // When doing reverse duck typing, we create a type that derives from typeToDeriveFrom (Original),
            // and overrides methods to call typeToDelegateTo (MyImplementation) (which is stored in an instance field) e.g.

            // public class Proxy: Original, IDuckType
            // {
            //     public object Instance {get;set;} // MyImplementation
            //     public virtual override SomeOverridenMethod() => Instance.SomeOverridenMethod();
            //     public virtual override OrigResult SomeOtherWithParams(OrigParam orig)
            //     {
            //         IDuckParam2 duck = DuckType.CreateCache<IDuckParam2>.Create(orig);
            //         IDuckResult result = Instance.SomeOtherWithParams(duck)
            //         return DuckType.CreateCache<OrigResult>.CreateReverse(result);
            //     }
            // }

            lock (Locker)
            {
                try
                {
                    // We can't reverse proxy a struct
                    if (typeToDeriveFrom.IsValueType)
                    {
                        return Failed(DuckTypeReverseProxyBaseIsStructException.Create(typeToDelegateTo));
                    }

                    // The "delegation" type can't be an interface for reverse proxy, as
                    // it needs to contain the implementations
                    if (typeToDelegateTo.IsInterface || typeToDelegateTo.IsAbstract)
                    {
                        return Failed(DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException.Create(typeToDeriveFrom));
                    }

                    ModuleBuilder? moduleBuilder = null;
                    TypeBuilder? proxyTypeBuilder = null;
                    FieldInfo? instanceField = null;

                    if (!dryRun)
                    {
                        moduleBuilder = CreateTypeAndModuleBuilder(typeToDeriveFrom, typeToDelegateTo, out proxyTypeBuilder, out instanceField);
                    }

                    // Create Fields and Properties
                    if (CreateReverseProxyProperties(proxyTypeBuilder, typeToDeriveFrom, typeToDelegateTo, instanceField) is { } propertyError)
                    {
                        return Failed(propertyError);
                    }

                    // Create Methods
                    if (CreateReverseProxyMethods(proxyTypeBuilder, typeToDeriveFrom, typeToDelegateTo, instanceField) is { } methodError)
                    {
                        return Failed(methodError);
                    }

                    if (AddCustomAttributes(proxyTypeBuilder, typeToDelegateTo, dryRun) is { } attributeError)
                    {
                        return Failed(attributeError);
                    }

                    if (dryRun)
                    {
                        // Dry run
                        return new CreateTypeResult(typeToDeriveFrom, null, typeToDelegateTo, null, exceptionInfo: null);
                    }

                    // Create Type
                    Type? proxyType = proxyTypeBuilder!.CreateTypeInfo()!.AsType();
                    return new CreateTypeResult(typeToDeriveFrom, proxyType, typeToDelegateTo, GetCreateProxyInstanceDelegate(moduleBuilder, typeToDeriveFrom, proxyType, typeToDelegateTo), exceptionInfo: null);
                }
                catch (DuckTypeException ex)
                {
                    return new CreateTypeResult(typeToDeriveFrom, null, typeToDelegateTo, null, ExceptionDispatchInfo.Capture(ex));
                }
                catch (Exception ex)
                {
                    // An unexpected fault from Reflection.Emit or reflection. Construct the wrapper instead of
                    // throwing and catching our own exception, so this path costs one first-chance exception
                    // rather than two.
                    return Failed(DuckTypeException.Create($"Error creating duck type for type: '{typeToDelegateTo}' using proxy: '{typeToDeriveFrom}'", ex));
                }

                CreateTypeResult Failed(DuckTypeException error)
                    => new CreateTypeResult(typeToDeriveFrom, null, typeToDelegateTo, null, ExceptionDispatchInfo.Capture(error));
            }
        }

        private static ModuleBuilder CreateTypeAndModuleBuilder(Type typeToDeriveFrom, Type typeToDelegateTo, out TypeBuilder proxyTypeBuilder, out FieldInfo instanceField)
        {
            // Define parent type, interface types
            Type parentType;
            TypeAttributes typeAttributes;
            Type[] interfaceTypes;

            var duckAsStruct = typeToDeriveFrom.IsValueType
                            || (typeToDeriveFrom.IsInterface && !HasGetAsClassAttribute(typeToDeriveFrom));

            if (duckAsStruct)
            {
                // If the proxy type definition is an interface we create a struct proxy unless explicitly marked as class
                // If the proxy type definition is an struct then we use that struct to copy the values from the target type
                parentType = typeof(ValueType);
                typeAttributes = TypeAttributes.Public | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.Serializable;
            }
            else
            {
                // If the proxy type definition is a class (or an interface that needs a class proxy) then we create a class proxy
                parentType = typeToDeriveFrom.IsInterface ? typeof(object) : typeToDeriveFrom;
                typeAttributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout | TypeAttributes.Sealed;
            }

            interfaceTypes = typeToDeriveFrom.IsInterface
                                 ? [typeToDeriveFrom, typeof(IDuckType)]
                                 : [typeof(IDuckType)];

            // Gets the module builder
            var moduleBuilder = GetModuleBuilder(typeToDelegateTo, (typeToDelegateTo.IsPublic || typeToDelegateTo.IsNestedPublic) && (typeToDeriveFrom.IsPublic || typeToDeriveFrom.IsNestedPublic));

            // Ensure visibility
            EnsureTypeVisibility(moduleBuilder, typeToDelegateTo);
            EnsureTypeVisibility(moduleBuilder, typeToDeriveFrom);

            // Create a "valid" type name (doesn't always hold) that can be used as a member of a class. (BenchmarkDotNet fails if is an invalid name)
            // The name we generate here is primarily for debugging purposes (stack traces etc), so we don't try too hard
            var proxyTypeNameSuffix = $"_{(++_typeCount).ToString(CultureInfo.InvariantCulture)}";
            var proxyTypeNamePrefix = GetProxyTypeNamePrefix(typeToDeriveFrom, typeToDelegateTo);

            // the maximum length for an assembly-qualified type name is 1024, so we need to account for that
            var maxPrefixSize = MaxProxyTypeNameLength - proxyTypeNameSuffix.Length;
            var proxyTypeName = (proxyTypeNamePrefix.Length > maxPrefixSize
                                     ? proxyTypeNamePrefix.Substring(0, maxPrefixSize)
                                     : proxyTypeNamePrefix)
                              + proxyTypeNameSuffix;

            proxyTypeBuilder = moduleBuilder.DefineType(
                proxyTypeName,
                typeAttributes,
                parentType,
                interfaceTypes);

            // Create IDuckType and IDuckTypeSetter implementations
            instanceField = CreateIDuckTypeImplementation(proxyTypeBuilder, typeToDelegateTo);

            // Define .ctor to store the instance field
            ConstructorBuilder ctorBuilder = proxyTypeBuilder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                new[] { instanceField.FieldType });
            ILGenerator ctorIL = ctorBuilder.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Ldarg_1);
            ctorIL.Emit(OpCodes.Stfld, instanceField);

            if (parentType == typeToDeriveFrom)
            {
                // The type initializer isn't a constructor the proxy can call.
                var proxyCtor = typeToDeriveFrom.GetTypeInfo().DeclaredConstructors.Where(pCtor => !pCtor.IsStatic && pCtor.GetParameters().Length == 0).FirstOrDefault();
                if (proxyCtor != null)
                {
                    ctorIL.Emit(OpCodes.Ldarg_0);
                    ctorIL.Emit(OpCodes.Call, proxyCtor);
                }
            }

            ctorIL.Emit(OpCodes.Ret);
            return moduleBuilder;

            static bool HasGetAsClassAttribute(Type interfaceProxy)
            {
                foreach (var attribute in interfaceProxy.GetCustomAttributes())
                {
                    if (attribute is DuckAsClassAttribute)
                    {
                        return true;
                    }

                    if (attribute is null)
                    {
                        continue;
                    }

                    // In case it's defined in Datadog.Trace.Manual etc
                    if (attribute.GetType().FullName == "Datadog.Trace.DuckTyping.DuckAsClassAttribute")
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the name of the proxy types created for a pair, before the counter that makes it unique.
        /// </summary>
        private static string GetProxyTypeNamePrefix(Type typeToDeriveFrom, Type typeToDelegateTo)
        {
            string assembly = string.Empty;
            if (typeToDelegateTo.Assembly is not null)
            {
                // Include target assembly name and public token.
                AssemblyName asmName = typeToDelegateTo.Assembly.GetName();
                assembly = asmName.Name ?? string.Empty;
                var pbToken = asmName.GetPublicKeyToken();
#if NET6_0_OR_GREATER
                assembly += "__" + (pbToken is null ? string.Empty : Convert.ToHexString(pbToken));
#else
                assembly += "__" + (pbToken is null ? string.Empty : HexConverter.ToString(pbToken));
#endif
                assembly = assembly.Replace(".", "_").Replace("+", "__");
            }

            return $"{assembly}.{GetProxyTypeNamePart(typeToDelegateTo)}.{GetProxyTypeNamePart(typeToDeriveFrom)}";
        }

        /// <summary>
        /// Gets how a type appears in the names of the proxy types created for it.
        /// </summary>
        private static string? GetProxyTypeNamePart(Type type) => type.FullName?.Replace(".", "_").Replace("+", "__");

        private static FieldBuilder CreateIDuckTypeImplementation(TypeBuilder proxyTypeBuilder, Type targetType)
        {
            Type instanceType = targetType;
            if (!UseDirectAccessTo(proxyTypeBuilder, targetType))
            {
                instanceType = typeof(object);
            }

            FieldBuilder instanceField = proxyTypeBuilder.DefineField("_currentInstance", instanceType, FieldAttributes.Private | FieldAttributes.InitOnly);

            PropertyBuilder propInstance = proxyTypeBuilder.DefineProperty(nameof(IDuckType.Instance), PropertyAttributes.None, typeof(object), null);
            MethodBuilder getPropInstance = proxyTypeBuilder.DefineMethod(
                "get_Instance",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                typeof(object),
                Type.EmptyTypes);
            ILGenerator il = getPropInstance.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, instanceField);
            if (instanceType.IsValueType)
            {
                il.Emit(OpCodes.Box, instanceType);
            }

            il.Emit(OpCodes.Ret);
            propInstance.SetGetMethod(getPropInstance);

            PropertyBuilder propType = proxyTypeBuilder.DefineProperty(nameof(IDuckType.Type), PropertyAttributes.None, typeof(Type), null);
            MethodBuilder getPropType = proxyTypeBuilder.DefineMethod(
                "get_Type",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                typeof(Type),
                Type.EmptyTypes);
            il = getPropType.GetILGenerator();
            il.Emit(OpCodes.Ldtoken, targetType);
            il.EmitCall(OpCodes.Call, GetTypeFromHandleMethodInfo, null);
            il.Emit(OpCodes.Ret);
            propType.SetGetMethod(getPropType);

            MethodBuilder getInstanceMethod = proxyTypeBuilder.DefineMethod(
                nameof(IDuckType.GetInternalDuckTypedInstance),
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot);
            var getInstanceGenericTypeParameters = getInstanceMethod.DefineGenericParameters("TReturn");
            getInstanceMethod.SetReturnType(getInstanceGenericTypeParameters[0].MakeByRefType());
            il = getInstanceMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldflda, instanceField);
            il.Emit(OpCodes.Ret);

            var toStringTargetType = targetType.GetMethod(nameof(IDuckType.ToString), Type.EmptyTypes);
            if (toStringTargetType is not null)
            {
                MethodBuilder toStringMethod = proxyTypeBuilder.DefineMethod(nameof(IDuckType.ToString), toStringTargetType.Attributes, typeof(string), Type.EmptyTypes);
                il = toStringMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                if (instanceType.IsValueType)
                {
                    il.Emit(OpCodes.Ldflda, instanceField);
                    il.Emit(OpCodes.Constrained, targetType);
                    il.EmitCall(OpCodes.Callvirt, toStringTargetType, null);
                }
                else
                {
                    il.Emit(OpCodes.Ldfld, instanceField);
                    il.Emit(OpCodes.Dup);
                    var lblTrue = il.DefineLabel();
                    il.Emit(OpCodes.Brtrue_S, lblTrue);

                    il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ldnull);
                    il.Emit(OpCodes.Ret);

                    il.MarkLabel(lblTrue);
                    il.EmitCall(OpCodes.Callvirt, toStringTargetType, null);
                }

                il.Emit(OpCodes.Ret);
            }

            return instanceField;
        }

        private static DuckTypeCustomAttributeHasNamedArgumentsException? AddCustomAttributes(TypeBuilder? proxyTypeBuilder, Type targetType, bool isDryRun)
        {
            foreach (var customAttributeData in targetType.GetCustomAttributesData())
            {
                var attributeType = customAttributeData.AttributeType;
                if (attributeType == typeof(DuckAttribute)
                 || attributeType == typeof(DuckCopyAttribute)
                 || attributeType == typeof(DuckFieldAttribute)
                 || attributeType == typeof(DuckIgnoreAttribute)
                 || attributeType == typeof(DuckIncludeAttribute)
                 || attributeType == typeof(DuckReverseMethodAttribute))
                {
                    continue;
                }

                // Don't support named arguments for now
                if (customAttributeData.NamedArguments?.Count > 0)
                {
                    return DuckTypeCustomAttributeHasNamedArgumentsException.Create(targetType, customAttributeData);
                }

                var args = Array.Empty<object?>();
                if (customAttributeData.ConstructorArguments.Count > 0)
                {
                    args = new object[customAttributeData.ConstructorArguments.Count];
                    for (var i = 0; i < customAttributeData.ConstructorArguments.Count; i++)
                    {
                        var arg = customAttributeData.ConstructorArguments[i];
                        args[i] = arg.Value;
                    }
                }

                var attributeBuilder = new CustomAttributeBuilder(customAttributeData.Constructor, constructorArgs: args);

                if (!isDryRun)
                {
                    proxyTypeBuilder?.SetCustomAttribute(attributeBuilder);
                }
            }

            return null;
        }

        /// <summary>
        /// Adds the properties of any implemented interfaces in <paramref name="proxyDefinitionType"/>
        /// to list <paramref name="selectedProperties"/> list
        /// </summary>
        /// <param name="proxyDefinitionType">The type to search the interfaces for</param>
        /// <param name="selectedProperties">Existing selected properties</param>
        private static void AddInterfaceProperties(Type proxyDefinitionType, List<PropertyInfo> selectedProperties)
        {
            Type[] implementedInterfaces = proxyDefinitionType.GetInterfaces();
            foreach (Type imInterface in implementedInterfaces)
            {
                if (imInterface == typeof(IDuckType))
                {
                    continue;
                }

                IEnumerable<PropertyInfo> newProps = imInterface.GetProperties().Where(p => selectedProperties.All(i => i.Name != p.Name));
                selectedProperties.AddRange(newProps);
            }
        }

        private static List<PropertyInfo> GetProperties(Type proxyDefinitionType)
        {
            List<PropertyInfo> selectedProperties = new List<PropertyInfo>(proxyDefinitionType.IsInterface ? proxyDefinitionType.GetProperties() : GetBaseProperties(proxyDefinitionType));
            AddInterfaceProperties(proxyDefinitionType, selectedProperties);

            return selectedProperties;

            static IEnumerable<PropertyInfo> GetBaseProperties(Type baseType)
            {
                foreach (PropertyInfo prop in baseType.GetProperties())
                {
                    if (prop.CanRead && prop.GetMethod is not null && (prop.GetMethod.IsAbstract || prop.GetMethod.IsVirtual))
                    {
                        yield return prop;
                    }
                    else if (prop.CanWrite && prop.SetMethod is not null && (prop.SetMethod.IsAbstract || prop.SetMethod.IsVirtual))
                    {
                        yield return prop;
                    }
                }
            }
        }

        private static List<PropertyInfo> GetReverseProperties(Type proxyDefinitionType)
        {
            List<PropertyInfo> selectedProperties = new List<PropertyInfo>();
            foreach (PropertyInfo prop in proxyDefinitionType.GetProperties())
            {
                if (prop.CanRead && prop.GetMethod is not null && prop.GetMethod.IsAbstract)
                {
                    selectedProperties.Add(prop);
                }
                else if (prop.CanWrite && prop.SetMethod is not null && prop.SetMethod.IsAbstract)
                {
                    selectedProperties.Add(prop);
                }
            }

            return selectedProperties;
        }

        /// <summary>
        /// Create properties in <paramref name="proxyTypeBuilder"/>
        /// </summary>
        /// <param name="proxyTypeBuilder">The type builder for the new proxy</param>
        /// <param name="proxyDefinitionType">The type we're inheriting from/implementing</param>
        /// <param name="targetType">The original type of the instance we're duck typing</param>
        /// <param name="instanceField">The field for accessing the instance of the <paramref name="targetType"/></param>
        private static DuckTypeException? CreateProperties(TypeBuilder? proxyTypeBuilder, Type proxyDefinitionType, Type targetType, FieldInfo? instanceField)
        {
            // Gets all properties to be implemented
            List<PropertyInfo> proxyTypeProperties = GetProperties(proxyDefinitionType);

            foreach (PropertyInfo proxyProperty in proxyTypeProperties)
            {
                // Ignore the properties marked with `DuckIgnore` attribute
                if (proxyProperty.GetCustomAttribute<DuckIgnoreAttribute>(true) is not null)
                {
                    continue;
                }

                // Check if proxy is a reverse method (shouldn't be called from here)
                if (proxyProperty.GetCustomAttribute<DuckReverseMethodAttribute>(true) is not null)
                {
                    return DuckTypeIncorrectReversePropertyUsageException.Create(proxyProperty);
                }

                PropertyBuilder? propertyBuilder = null;

                DuckAttribute duckAttribute = proxyProperty.GetCustomAttribute<DuckAttribute>(true) ?? new DuckAttribute();
                duckAttribute.Name ??= proxyProperty.Name;

                switch (duckAttribute.Kind)
                {
                    case DuckKind.Property:
                    case DuckKind.PropertyOrField:
                        PropertyInfo? targetProperty = FindForwardTargetProperty(targetType, duckAttribute, duckAttribute.Name, proxyProperty);

                        if (targetProperty is null)
                        {
                            if (duckAttribute.Kind == DuckKind.PropertyOrField)
                            {
                                goto case DuckKind.Field;
                            }

                            if (proxyProperty.CanRead && proxyProperty.GetMethod is not null)
                            {
                                var getMethod = proxyProperty.GetMethod;
                                if (getMethod.IsAbstract || getMethod.IsVirtual)
                                {
                                    return DuckTypePropertyOrFieldNotFoundException.Create(proxyProperty.Name, duckAttribute.Name, targetType);
                                }
                            }

                            if (proxyProperty.CanWrite && proxyProperty.SetMethod is not null)
                            {
                                var setMethod = proxyProperty.SetMethod;
                                if (setMethod.IsAbstract || setMethod.IsVirtual)
                                {
                                    return DuckTypePropertyOrFieldNotFoundException.Create(proxyProperty.Name, duckAttribute.Name, targetType);
                                }
                            }

                            continue;
                        }

                        propertyBuilder = proxyTypeBuilder?.DefineProperty(proxyProperty.Name, PropertyAttributes.None, proxyProperty.PropertyType, null);

                        if (proxyProperty.CanRead)
                        {
                            // Check if the target property can be read
                            if (!targetProperty.CanRead)
                            {
                                return DuckTypePropertyCantBeReadException.Create(targetProperty);
                            }

                            if (GetPropertyGetMethod(
                                proxyTypeBuilder,
                                targetType: targetType,
                                proxyMember: proxyProperty,
                                targetProperty: targetProperty,
                                instanceField: instanceField,
                                duckCastInnerToOuterFunc: MethodIlHelper.AddIlToDuckChain,
                                needsDuckChaining: NeedsDuckChaining,
                                proxyMethodResult: out var getMethodBuilder) is { } getError)
                            {
                                return getError;
                            }

                            if (getMethodBuilder is not null)
                            {
                                propertyBuilder?.SetGetMethod(getMethodBuilder);
                            }
                        }

                        if (proxyProperty.CanWrite)
                        {
                            // Check if the target property can be written
                            if (!targetProperty.CanWrite)
                            {
                                return DuckTypePropertyCantBeWrittenException.Create(targetProperty);
                            }

                            // Check if the target property declaring type is an struct (structs modification is not supported)
                            if (targetProperty.DeclaringType?.IsValueType == true)
                            {
                                return DuckTypeStructMembersCannotBeChangedException.Create(targetProperty.DeclaringType);
                            }

                            if (GetPropertySetMethod(
                                proxyTypeBuilder,
                                targetType: targetType,
                                proxyMember: proxyProperty,
                                targetProperty: targetProperty,
                                instanceField: instanceField,
                                proxyMethodResult: out var setMethodBuilder,
                                duckCastOuterToInner: MethodIlHelper.AddIlToExtractDuckType,
                                needsDuckChaining: NeedsDuckChaining) is { } setError)
                            {
                                return setError;
                            }

                            if (setMethodBuilder is not null)
                            {
                                propertyBuilder?.SetSetMethod(setMethodBuilder);
                            }
                        }

                        break;

                    case DuckKind.Field:
                        FieldInfo? targetField = FindTargetField(targetType, duckAttribute, duckAttribute.Name);

                        if (targetField is null)
                        {
                            return DuckTypePropertyOrFieldNotFoundException.Create(proxyProperty.Name, duckAttribute.Name, targetType);
                        }

                        propertyBuilder = proxyTypeBuilder?.DefineProperty(proxyProperty.Name, PropertyAttributes.None, proxyProperty.PropertyType, null);

                        if (proxyProperty.CanRead)
                        {
                            if (GetFieldGetMethod(proxyTypeBuilder, targetType, proxyProperty, targetField, instanceField, out var getMethodBuilder) is { } getError)
                            {
                                return getError;
                            }

                            if (getMethodBuilder is not null)
                            {
                                propertyBuilder?.SetGetMethod(getMethodBuilder);
                            }
                        }

                        if (proxyProperty.CanWrite)
                        {
                            // Check if the target field is marked as InitOnly (readonly) and throw an exception in that case
                            if ((targetField.Attributes & FieldAttributes.InitOnly) != 0)
                            {
                                return DuckTypeFieldIsReadonlyException.Create(targetField);
                            }

                            // Check if the target field declaring type is an struct (structs modification is not supported)
                            if (targetField.DeclaringType?.IsValueType == true)
                            {
                                return DuckTypeStructMembersCannotBeChangedException.Create(targetField.DeclaringType);
                            }

                            if (GetFieldSetMethod(proxyTypeBuilder, targetType, proxyProperty, targetField, instanceField, out var setMethodBuilder) is { } setError)
                            {
                                return setError;
                            }

                            if (setMethodBuilder is not null)
                            {
                                propertyBuilder?.SetSetMethod(setMethodBuilder);
                            }
                        }

                        break;
                }
            }

            return null;
        }

        /// <summary>
        /// Create properties in <paramref name="proxyTypeBuilder"/>
        /// </summary>
        /// <param name="proxyTypeBuilder">The type builder for the new proxy</param>
        /// <param name="typeToDeriveFrom">The type we're inheriting from/implementing</param>
        /// <param name="typeToDelegateTo">The type we're delegating the implementation too</param>
        /// <param name="instanceField">The field for accessing the instance of the <paramref name="typeToDelegateTo"/></param>
        private static DuckTypeException? CreateReverseProxyProperties(TypeBuilder? proxyTypeBuilder, Type typeToDeriveFrom, Type typeToDelegateTo, FieldInfo? instanceField)
        {
            var propertiesThatShouldBeImplemented = GetReverseProperties(typeToDeriveFrom);

            // Get all the properties on our delegation type that we're going to delegate to
            // Note that these don't need to be abstract/virtual, unlike in a normal (forward) proxy
            List<PropertyInfo> delegationTypeProperties = new List<PropertyInfo>(typeToDelegateTo.GetProperties());

            foreach (PropertyInfo implementationProperty in delegationTypeProperties)
            {
                // Ignore methods without a `DuckReverse` attribute
                if (implementationProperty.GetCustomAttribute<DuckReverseMethodAttribute>(true) is null)
                {
                    continue;
                }

                PropertyBuilder? propertyBuilder = null;

                DuckReverseMethodAttribute duckAttribute = implementationProperty.GetCustomAttribute<DuckReverseMethodAttribute>(true) ?? new DuckReverseMethodAttribute();
                duckAttribute.Name ??= implementationProperty.Name;

                // The "implementor" property cannot be abstract or interface if we're doing a reverse proxy
                if ((implementationProperty.CanRead && implementationProperty.GetMethod?.IsAbstract == true)
                 || (implementationProperty.CanWrite && implementationProperty.SetMethod?.IsAbstract == true))
                {
                    // Unreachable: line 292 above rejects an interface or abstract typeToDelegateTo, and only those
                    // can declare an abstract member. Kept as a defensive invariant check.
                    return DuckTypeReverseProxyPropertyCannotBeAbstractException.Create(implementationProperty);
                }

                PropertyInfo? overriddenProperty = GetTargetPropertyOrIndex(typeToDeriveFrom, duckAttribute.Name, duckAttribute.BindingFlags, implementationProperty, duckAttribute.ExplicitInterfaceTypeName);
                if (overriddenProperty is null)
                {
                    return DuckTypePropertyOrFieldNotFoundException.Create(implementationProperty.Name, duckAttribute.Name, typeToDeriveFrom);
                }

                // Named after the delegation's property, with the type of the property it implements (its accessors' type).
                propertyBuilder = proxyTypeBuilder?.DefineProperty(implementationProperty.Name, PropertyAttributes.None, overriddenProperty.PropertyType, null);

                if (implementationProperty.CanRead)
                {
                    // Check if the target property can be read
                    if (!overriddenProperty.CanRead)
                    {
                        return DuckTypePropertyCantBeReadException.Create(overriddenProperty);
                    }

                    if (GetPropertyGetMethod(
                        proxyTypeBuilder,
                        targetType: typeToDeriveFrom,
                        proxyMember: overriddenProperty,
                        targetProperty: implementationProperty,
                        instanceField: instanceField,
                        duckCastInnerToOuterFunc: MethodIlHelper.AddIlToExtractDuckType,
                        needsDuckChaining: MethodIlHelper.NeedsDuckChainingReverse,
                        proxyMethodResult: out var getMethodBuilder) is { } getError)
                    {
                        return getError;
                    }

                    if (getMethodBuilder is not null)
                    {
                        propertyBuilder?.SetGetMethod(getMethodBuilder);
                    }
                }

                if (implementationProperty.CanWrite)
                {
                    // Check if the target property can be written
                    if (!overriddenProperty.CanWrite)
                    {
                        return DuckTypePropertyCantBeWrittenException.Create(overriddenProperty);
                    }

                    // Check if the target property declaring type is an struct (structs modification is not supported)
                    if (overriddenProperty.DeclaringType?.IsValueType == true)
                    {
                        return DuckTypeStructMembersCannotBeChangedException.Create(overriddenProperty.DeclaringType);
                    }

                    if (GetPropertySetMethod(
                        proxyTypeBuilder,
                        targetType: typeToDeriveFrom,
                        proxyMember: overriddenProperty,
                        targetProperty: implementationProperty,
                        instanceField: instanceField,
                        proxyMethodResult: out var setMethodBuilder,
                        duckCastOuterToInner: MethodIlHelper.AddIlToDuckChain,
                        needsDuckChaining: MethodIlHelper.NeedsDuckChainingReverse) is { } setError)
                    {
                        return setError;
                    }

                    if (setMethodBuilder is not null)
                    {
                        propertyBuilder?.SetSetMethod(setMethodBuilder);
                    }
                }

                propertiesThatShouldBeImplemented.RemoveAll(prop => GetDuckAttributeCandidateNames(duckAttribute.Name).Any(name => name == prop.Name));
            }

            if (propertiesThatShouldBeImplemented.Count > 0)
            {
                return DuckTypeReverseProxyMissingPropertyImplementationException.Create(propertiesThatShouldBeImplemented);
            }

            return null;
        }

        /// <summary>
        /// Create properties in <paramref name="proxyTypeBuilder"/>
        /// </summary>
        /// <param name="proxyTypeBuilder">The type builder for the new proxy</param>
        /// <param name="proxyDefinitionType">The custom type we defined</param>
        /// <param name="targetType">The original type we are proxying</param>
        /// <param name="instanceField">The field for accessing the instance of the <paramref name="targetType"/></param>
        private static DuckTypeException? CreatePropertiesFromStruct(TypeBuilder? proxyTypeBuilder, Type proxyDefinitionType, Type targetType, FieldInfo? instanceField)
        {
            var containsFields = false;

            // Gets all fields to be copied
            foreach (FieldInfo proxyFieldInfo in proxyDefinitionType.GetFields())
            {
                // Skip readonly fields
                if ((proxyFieldInfo.Attributes & FieldAttributes.InitOnly) != 0)
                {
                    continue;
                }

                // Ignore the fields marked with `DuckIgnore` attribute
                if (proxyFieldInfo.GetCustomAttribute<DuckIgnoreAttribute>(true) is not null)
                {
                    continue;
                }

                // Any field that gets this far either has a getter generated for it below, or makes the
                // whole proxy fail - so reaching here is what CreateStructCopyMethod means by "contains
                // fields".
                containsFields = true;

                PropertyBuilder? propertyBuilder = null;
                MethodBuilder? getMethodBuilder = null;

                DuckAttribute duckAttribute = proxyFieldInfo.GetCustomAttribute<DuckAttribute>(true) ?? new DuckAttribute();
                duckAttribute.Name ??= proxyFieldInfo.Name;

                switch (duckAttribute.Kind)
                {
                    case DuckKind.Property:
                    case DuckKind.PropertyOrField:
                        if (FindDuckCopyTargetProperty(targetType, duckAttribute, duckAttribute.Name, out PropertyInfo? targetProperty) is { } propertyError)
                        {
                            return propertyError;
                        }

                        if (targetProperty is null)
                        {
                            if (duckAttribute.Kind == DuckKind.PropertyOrField)
                            {
                                goto case DuckKind.Field;
                            }

                            return DuckTypePropertyOrFieldNotFoundException.Create(proxyFieldInfo.Name, duckAttribute.Name, targetType);
                        }

                        // Check if the target property can be read
                        if (!targetProperty.CanRead)
                        {
                            return DuckTypePropertyCantBeReadException.Create(targetProperty);
                        }

                        propertyBuilder = proxyTypeBuilder?.DefineProperty(proxyFieldInfo.Name, PropertyAttributes.None, proxyFieldInfo.FieldType, null);

                        if (GetPropertyGetMethod(
                            proxyTypeBuilder,
                            targetType: targetType,
                            proxyMember: proxyFieldInfo,
                            targetProperty: targetProperty,
                            instanceField: instanceField,
                            duckCastInnerToOuterFunc: MethodIlHelper.AddIlToDuckChain,
                            needsDuckChaining: NeedsDuckChaining,
                            proxyMethodResult: out getMethodBuilder) is { } getError)
                        {
                            return getError;
                        }

                        if (getMethodBuilder is not null)
                        {
                            propertyBuilder?.SetGetMethod(getMethodBuilder);
                        }

                        break;

                    case DuckKind.Field:
                        FieldInfo? targetField = FindTargetField(targetType, duckAttribute, duckAttribute.Name);

                        if (targetField is null)
                        {
                            return DuckTypePropertyOrFieldNotFoundException.Create(proxyFieldInfo.Name, duckAttribute.Name, targetType);
                        }

                        propertyBuilder = proxyTypeBuilder?.DefineProperty(proxyFieldInfo.Name, PropertyAttributes.None, proxyFieldInfo.FieldType, null);
                        if (GetFieldGetMethod(proxyTypeBuilder, targetType, proxyFieldInfo, targetField, instanceField, out getMethodBuilder) is { } fieldGetError)
                        {
                            return fieldGetError;
                        }

                        if (getMethodBuilder is not null)
                        {
                            propertyBuilder?.SetGetMethod(getMethodBuilder);
                        }

                        break;
                }
            }

            // A [DuckCopy] proxy copies values into fields, so one declaring only properties can never be
            // populated. Checked here so that it fails on the dry run too
            if (!containsFields && proxyDefinitionType.GetProperties().Length != 0)
            {
                return DuckTypeDuckCopyStructDoesNotContainsAnyField.Create(proxyDefinitionType);
            }

            return null;
        }

        private static Delegate GetCreateProxyInstanceDelegate(ModuleBuilder? moduleBuilder, Type proxyDefinitionType, Type proxyType, Type targetType)
        {
            ConstructorInfo ctor = proxyType.GetConstructors()[0];

            DynamicMethod createProxyMethod = new DynamicMethod(
                $"CreateProxyInstance<{proxyType.Name}>",
                proxyDefinitionType,
                new[] { typeof(object) },
                typeof(DuckType).Module,
                true);
            ILGenerator il = createProxyMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (UseDirectAccessTo(moduleBuilder, targetType))
            {
                if (targetType.IsValueType)
                {
                    il.Emit(OpCodes.Unbox_Any, targetType);
                }
                else
                {
                    il.Emit(OpCodes.Castclass, targetType);
                }
            }

            il.Emit(OpCodes.Newobj, ctor);

            if (proxyType.IsValueType)
            {
                il.Emit(OpCodes.Box, proxyType);
            }

            il.Emit(OpCodes.Ret);
            Type delegateType = typeof(CreateProxyInstance<>).MakeGenericType(proxyDefinitionType);
            return createProxyMethod.CreateDelegate(delegateType);
        }

        private static DuckTypeDuckCopyStructDoesNotContainsAnyField? CreateStructCopyMethod(ModuleBuilder? moduleBuilder, Type proxyDefinitionType, Type proxyType, Type targetType, out Delegate? activator)
        {
            activator = null;
            ConstructorInfo ctor = proxyType.GetConstructors()[0];

            DynamicMethod createStructMethod = new DynamicMethod(
                $"CreateStructInstance<{proxyType.Name}>",
                proxyDefinitionType,
                new[] { typeof(object) },
                typeof(DuckType).Module,
                true);
            ILGenerator il = createStructMethod.GetILGenerator();

            // First we declare the locals
            LocalBuilder proxyLocal = il.DeclareLocal(proxyType);
            LocalBuilder structLocal = il.DeclareLocal(proxyDefinitionType);

            // We create an instance of the proxy type
            il.Emit(OpCodes.Ldloca_S, proxyLocal);
            il.Emit(OpCodes.Ldarg_0);
            if (UseDirectAccessTo(moduleBuilder, targetType))
            {
                il.Emit(targetType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, targetType);
            }

            il.Emit(OpCodes.Call, ctor);

            // Create the destination structure
            il.Emit(OpCodes.Ldloca_S, structLocal);
            il.Emit(OpCodes.Initobj, proxyDefinitionType);

            // Start copy properties from the proxy to the structure
            bool containsFields = false;
            foreach (var finfo in proxyDefinitionType.GetFields())
            {
                // Skip readonly fields
                if ((finfo.Attributes & FieldAttributes.InitOnly) != 0)
                {
                    continue;
                }

                // Ignore the fields marked with `DuckIgnore` attribute
                if (finfo.GetCustomAttribute<DuckIgnoreAttribute>(true) is not null)
                {
                    continue;
                }

                if (proxyType.GetProperty(finfo.Name) is { GetMethod: { } propGetMethod })
                {
                    il.Emit(OpCodes.Ldloca_S, structLocal);
                    il.Emit(OpCodes.Ldloca_S, proxyLocal);
                    il.EmitCall(OpCodes.Call, propGetMethod, null);
                    il.Emit(OpCodes.Stfld, finfo);
                    containsFields = true;
                }
            }

            // Return
            il.WriteLoadLocal(structLocal.LocalIndex);
            il.Emit(OpCodes.Ret);

            // Now unreachable: CreatePropertiesFromStruct makes the same check on both legs, and this method
            // only runs after that succeeded. Kept as a defensive guard.
            if (!containsFields && proxyDefinitionType.GetProperties().Length != 0)
            {
                return DuckTypeDuckCopyStructDoesNotContainsAnyField.Create(proxyDefinitionType);
            }

            Type delegateType = typeof(CreateProxyInstance<>).MakeGenericType(proxyDefinitionType);
            activator = createStructMethod.CreateDelegate(delegateType);
            return null;
        }

        /// <summary>
        /// Finds the target property a forward proxy property binds to, walking the base types with FallbackToBaseTypes.
        /// </summary>
        private static PropertyInfo? FindForwardTargetProperty(Type targetType, DuckAttribute duckAttribute, string name, PropertyInfo proxyProperty)
        {
            var targetProperty = GetTargetPropertyOrIndex(targetType, name, duckAttribute.BindingFlags, proxyProperty, duckAttribute.ExplicitInterfaceTypeName);
            if (duckAttribute.FallbackToBaseTypes)
            {
                var currentType = targetType;
                while (targetProperty is null && currentType is { IsValueType: false, BaseType: not null } && currentType.BaseType != typeof(object))
                {
                    currentType = currentType.BaseType;
                    targetProperty = GetTargetPropertyOrIndex(currentType, name, duckAttribute.BindingFlags, proxyProperty, duckAttribute.ExplicitInterfaceTypeName);
                }
            }

            return targetProperty;
        }

        /// <summary>
        /// Finds the target property a [DuckCopy] struct field binds to, walking the base types with FallbackToBaseTypes.
        /// </summary>
        private static DuckTypeException? FindDuckCopyTargetProperty(Type targetType, DuckAttribute duckAttribute, string name, out PropertyInfo? targetProperty)
        {
            if (GetTargetProperty(targetType, name, duckAttribute.BindingFlags, out targetProperty, duckAttribute.ExplicitInterfaceTypeName) is { } propertyError)
            {
                return propertyError;
            }

            if (duckAttribute.FallbackToBaseTypes)
            {
                var currentType = targetType;
                while (targetProperty is null && currentType is { IsValueType: false, BaseType: not null } && currentType.BaseType != typeof(object))
                {
                    currentType = currentType.BaseType;
                    if (GetTargetProperty(currentType, name, duckAttribute.BindingFlags, out targetProperty, duckAttribute.ExplicitInterfaceTypeName) is { } baseTypeError)
                    {
                        return baseTypeError;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the target field a proxy member binds to, walking the base types with FallbackToBaseTypes.
        /// </summary>
        private static FieldInfo? FindTargetField(Type targetType, DuckAttribute duckAttribute, string name)
        {
            var targetField = GetTargetField(targetType, name, duckAttribute.BindingFlags);
            if (duckAttribute.FallbackToBaseTypes)
            {
                var currentType = targetType;
                while (targetField is null && currentType is { IsValueType: false, BaseType: not null } && currentType.BaseType != typeof(object))
                {
                    currentType = currentType.BaseType;
                    targetField = GetTargetField(currentType, name, duckAttribute.BindingFlags);
                }
            }

            return targetField;
        }

        private static PropertyInfo? GetTargetPropertyOrIndex(Type targetType, string propertyName, BindingFlags bindingFlags, PropertyInfo proxyPropertyInfo, string? explicitInterfaceTypeName = null)
        {
            if (propertyName.IndexOf(',') == -1)
            {
                return FindPropertyOrIndex(targetType, propertyName, bindingFlags, proxyPropertyInfo, explicitInterfaceTypeName);
            }

            PropertyInfo? targetProperty = null;
            foreach (var name in GetDuckAttributeCandidateNames(propertyName))
            {
                targetProperty = FindPropertyOrIndex(targetType, name, bindingFlags, proxyPropertyInfo, explicitInterfaceTypeName);

                if (targetProperty is not null)
                {
                    break;
                }
            }

            return targetProperty;

            static PropertyInfo? FindPropertyOrIndex(Type targetType, string propertyName, BindingFlags bindingFlags, PropertyInfo proxyPropertyInfo, string? explicitInterfaceTypeName)
            {
                // Avoid calling GetProperty(propertyName, bindingFlags) so that we avoid throwing when we have multiple indexers
                var candidates = GetPropertyCandidates(targetType, propertyName, bindingFlags, explicitInterfaceTypeName);

                if (candidates.Length == 0)
                {
                    return null;
                }

                if (candidates.Length == 1)
                {
                    return (PropertyInfo)candidates[0];
                }

                // More than one property carries this name, which is where Type.GetProperty(name, flags) gives
                // up by throwing. Can happen if the target declares several indexers, or hides a base property with
                // a different signature.
                var indexParameters = proxyPropertyInfo.GetIndexParameters();
                if (!StringUtil.IsNullOrEmpty(explicitInterfaceTypeName) && !string.Equals(candidates[0].Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    // Explicit implementations, named with their interface (properties with the plain name, in any case with
                    // IgnoreCase, win and follow the rules below): wildcard interface names can select different properties with
                    // the same simple name. Only an unambiguous exact signature is safe; do not throw during dry-run validation.
                    var matchingProperties = candidates.Cast<PropertyInfo>().Where(property => SignatureMatches(property, proxyPropertyInfo.PropertyType, indexParameters)).ToArray();
                    return matchingProperties.Length == 1 ? matchingProperties[0] : null;
                }

                if (indexParameters.Length == 0)
                {
                    // fallback, could happen if you use "new", just accept the gap
                    return targetType.GetProperty(propertyName, proxyPropertyInfo.PropertyType);
                }

                // Indexers only. Try to find the one with an exact match for parameters
                foreach (var candidate in candidates)
                {
                    var property = (PropertyInfo)candidate;
                    if (SignatureMatches(property, proxyPropertyInfo.PropertyType, indexParameters))
                    {
                        return property;
                    }
                }

                // There wasn't an exact type match despite multiple indexers. Now we just YOLO and except this could throw
                var parameterTypes = new Type[indexParameters.Length];
                for (var i = 0; i < indexParameters.Length; i++)
                {
                    parameterTypes[i] = indexParameters[i].ParameterType;
                }

                return targetType.GetProperty(propertyName, proxyPropertyInfo.PropertyType, parameterTypes);
            }

            static bool SignatureMatches(PropertyInfo candidate, Type propertyType, ParameterInfo[] indexParameters)
            {
                if (candidate.PropertyType != propertyType)
                {
                    return false;
                }

                var candidateParameters = candidate.GetIndexParameters();
                if (candidateParameters.Length != indexParameters.Length)
                {
                    return false;
                }

                for (var i = 0; i < candidateParameters.Length; i++)
                {
                    if (candidateParameters[i].ParameterType != indexParameters[i].ParameterType)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static DuckTypeException? GetTargetProperty(Type targetType, string propertyName, BindingFlags bindingFlags, out PropertyInfo? targetProperty, string? explicitInterfaceTypeName = null)
        {
            if (propertyName.IndexOf(',') == -1)
            {
                return FindProperty(targetType, propertyName, bindingFlags, out targetProperty, explicitInterfaceTypeName);
            }

            targetProperty = null;
            foreach (var name in GetDuckAttributeCandidateNames(propertyName))
            {
                if (FindProperty(targetType, name, bindingFlags, out targetProperty, explicitInterfaceTypeName) is { } error)
                {
                    return error;
                }

                if (targetProperty is not null)
                {
                    break;
                }
            }

            return null;

            static DuckTypeException? FindProperty(Type targetType, string propertyName, BindingFlags bindingFlags, out PropertyInfo? property, string? explicitInterfaceTypeName)
            {
                var candidates = GetPropertyCandidates(targetType, propertyName, bindingFlags, explicitInterfaceTypeName);

                if (candidates.Length > 1)
                {
                    property = null;
                    return DuckTypeTargetPropertyAmbiguousMatchException.Create(targetType, propertyName);
                }

                property = candidates.Length == 0 ? null : (PropertyInfo)candidates[0];
                return null;
            }
        }

        private static MemberInfo[] GetPropertyCandidates(Type targetType, string propertyName, BindingFlags bindingFlags, string? explicitInterfaceTypeName = null)
        {
            // A trailing '*' means "starts with" to GetMember, whereas Type.GetProperty compares the name literally.
            // We only support exact matches, so explicitly don't find these members
            if (propertyName.Length > 0 && propertyName[propertyName.Length - 1] == '*')
            {
                return [];
            }

            // Like methods (Type.GetMethod runs before the explicit interface scan), a property with the plain name wins:
            // ExplicitInterfaceTypeName only adds explicit implementations when there's none. Properties always bound that
            // way, e.g. protoc messages bind their public static Descriptor, not pb::IMessage.Descriptor.
            var candidates = targetType.GetMember(propertyName, MemberTypes.Property, bindingFlags);
            if (candidates.Length > 0 || StringUtil.IsNullOrEmpty(explicitInterfaceTypeName))
            {
                return candidates;
            }

            if (explicitInterfaceTypeName == "*")
            {
                var comparison = (bindingFlags & BindingFlags.IgnoreCase) != 0 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return targetType.GetProperties(bindingFlags)
                                 .Where(property => property.Name.EndsWith("." + propertyName, comparison))
                                 .Cast<MemberInfo>()
                                 .ToArray();
            }

            // Nested types are separated with a "." on explicit implementations.
            return targetType.GetMember(explicitInterfaceTypeName!.Replace("+", ".") + "." + propertyName, MemberTypes.Property, bindingFlags);
        }

        private static FieldInfo? GetTargetField(Type targetType, string fieldName, BindingFlags bindingFlags)
        {
            if (fieldName.IndexOf(',') == -1)
            {
                return targetType.GetField(fieldName, bindingFlags);
            }

            FieldInfo? targetField = null;
            foreach (var name in GetDuckAttributeCandidateNames(fieldName))
            {
                targetField = targetType.GetField(name, bindingFlags);
                if (targetField is not null)
                {
                    break;
                }
            }

            return targetField;
        }

        /// <summary>
        /// Struct to store the result of creating a proxy type
        /// </summary>
        public readonly struct CreateTypeResult
        {
            /// <summary>
            /// Gets if the proxy type creation was successful
            /// </summary>
            public readonly bool Success;

            /// <summary>
            /// Target type
            /// </summary>
            public readonly Type? TargetType;

            private readonly Type? _proxyType;
            private readonly Delegate? _activator;

            // The failure, if any: an ExceptionDispatchInfo, or the Action an AOT failure registration throws with. One field
            // keeps this struct as small as it was before AOT support. The activator holds it too (see the constructor).
            private readonly object? _failure;

            /// <summary>
            /// Initializes a new instance of the <see cref="CreateTypeResult"/> struct.
            /// </summary>
            /// <param name="proxyTypeDefinition">Proxy type definition</param>
            /// <param name="proxyType">Proxy type</param>
            /// <param name="targetType">Target type</param>
            /// <param name="activator">Proxy activator</param>
            /// <param name="exceptionInfo">Exception dispatch info instance</param>
            internal CreateTypeResult(Type proxyTypeDefinition, Type? proxyType, Type targetType, Delegate? activator, ExceptionDispatchInfo? exceptionInfo)
                : this(proxyType, targetType, activator, failure: exceptionInfo, proxyTypeDefinition)
            {
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="CreateTypeResult"/> struct using a deferred failure thrower.
            /// </summary>
            /// <param name="proxyTypeDefinition">Proxy type definition</param>
            /// <param name="proxyType">Proxy type</param>
            /// <param name="targetType">Target type</param>
            /// <param name="activator">Proxy activator</param>
            /// <param name="failureThrower">Failure thrower instance</param>
            internal CreateTypeResult(Type proxyTypeDefinition, Type? proxyType, Type targetType, Delegate? activator, Action? failureThrower)
                : this(proxyType, targetType, activator, failure: failureThrower, proxyTypeDefinition)
            {
            }

            private CreateTypeResult(Type? proxyType, Type targetType, Delegate? activator, object? failure, Type? proxyTypeDefinition)
            {
                // Generated (AOT) activators are rebound to Func<object, object> once, so object-based creation never needs
                // DynamicInvoke, which NativeAOT may not support. Dynamic methods can't be rebound: they keep their typed delegate.
                // A failure is also kept in the activator slot, as an Action that throws it: CreateInstance<T> then reads a single
                // field, which lets the JIT read it from the fast path entry instead of copying this struct.
                // The typed activator of a registry, bound to its object activator, is kept: it serves both (see CreateInstance).
                _activator = failure is not null ? CreateFailureThrower(failure)
                           : activator is null or Func<object?, object?> || activator.Target is Func<object?, object?> ? activator : TryCreateObjectActivator(activator) ?? activator;

                // An object activator knows the proxy definition type, which CreateInstance<T> requires as T, like the typed
                // activator of dynamic duck typing (see CreateInstanceSlow).
                if (proxyTypeDefinition is not null && _activator is Func<object?, object?> { Target: not ObjectActivator } objectActivator)
                {
                    _activator = new Func<object?, object?>(new ObjectActivator(proxyTypeDefinition, objectActivator).Create);
                }

                _proxyType = proxyType;
                _failure = failure;
                TargetType = targetType;
                Success = proxyType is not null && failure is null;
            }

            /// <summary>
            /// Gets the Proxy type
            /// </summary>
            public Type? ProxyType
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    ThrowFailureIfNeeded();
                    return _proxyType;
                }
            }

            /// <summary>
            /// Gets a value indicating whether object-based creation has to fall back to DynamicInvoke.
            /// </summary>
            internal bool UsesDynamicInvokeFallback => _failure is null && _activator is not null and not Func<object?, object?> && _activator.Target is not Func<object?, object?>;

            /// <summary>
            /// Gets the exception creating a proxy throws, or null when the proxy type can be created.
            /// </summary>
            internal Exception? FailureException
            {
                get
                {
                    if (_failure is ExceptionDispatchInfo exceptionInfo)
                    {
                        return exceptionInfo.SourceException;
                    }

                    if (_failure is null)
                    {
                        return null;
                    }

                    try
                    {
                        ThrowFailure(_failure);
                        return null;
                    }
                    catch (Exception ex)
                    {
                        return ex;
                    }
                }
            }

            /// <summary>
            /// Gets the same result for another target type, with another activator when it succeeds: an AOT proxy of an array
            /// type also serves the array types assignable to it.
            /// </summary>
            /// <param name="targetType">The target type.</param>
            /// <param name="activator">The activator for the target type, or null to keep this one.</param>
            /// <returns>The result for the target type.</returns>
            internal CreateTypeResult WithTargetType(Type targetType, Delegate? activator)
                => new(_proxyType, targetType, _failure is null ? activator ?? _activator : null, _failure, GetProxyTypeDefinition(_activator));

            /// <summary>
            /// Create a new proxy instance from a target instance
            /// </summary>
            /// <typeparam name="T">Type of the return value</typeparam>
            /// <param name="instance">Target instance value</param>
            /// <returns>Proxy instance</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [return: NotNull]
            public T CreateInstance<T>(object? instance)
            {
                // Only the activator is read (a failure is an Action there, see the constructor): reading other fields, or an
                // instance call, makes callers copy this struct out of the fast path entry on every call.
                var activator = _activator;
                if (activator is CreateProxyInstance<T> typedActivator)
                {
                    return typedActivator(instance);
                }

                return CreateInstanceSlow<T>(activator, instance);
            }

            /// <summary>
            /// Create a new proxy instance from a target instance
            /// </summary>
            /// <typeparam name="T">Type of the return value</typeparam>
            /// <typeparam name="TOriginal">Type of the original value</typeparam>
            /// <param name="instance">Target instance value</param>
            /// <returns>Proxy instance</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [return: NotNull]
            public T CreateInstance<T, TOriginal>(TOriginal instance)
            {
                var activator = _activator;
                if (activator is CreateProxyInstance<T> typedActivator)
                {
                    return typedActivator(instance);
                }

                return CreateInstanceSlow<T>(activator, instance);
            }

            /// <summary>
            /// Get if the proxy instance can be created
            /// </summary>
            /// <returns>true if the proxy can be created; otherwise, false.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool CanCreate()
            {
                return _failure is null;
            }

            // Not inlined: its callers (Create(Type, object), DuckAs, TryDuckCast...) don't grow with the AOT cases, and the call
            // is negligible next to DynamicInvoke.
            [MethodImpl(MethodImplOptions.NoInlining)]
            internal object CreateInstance(object instance)
            {
                // Dynamic duck typing creates object-based proxies through DynamicInvoke, so failures and activator exceptions
                // surface wrapped in a TargetInvocationException: AOT results keep that contract.
                if (_failure is not null)
                {
                    ThrowFailureAsTargetInvocationException();
                }

                if (_activator is Func<object?, object?> objectActivator)
                {
                    return InvokeObjectActivator(objectActivator, instance);
                }

                // The typed activator of a registry is bound to its object activator.
                if (_activator?.Target is Func<object?, object?> boundObjectActivator)
                {
                    return InvokeObjectActivator(boundObjectActivator, instance);
                }

                if (_activator is null)
                {
                    ThrowHelper.ThrowNullReferenceException("The activator for this proxy type is null, check if the type can be created by calling 'CanCreate()'");
                }

                return _activator.DynamicInvoke(instance)!;
            }

            /// <summary>
            /// Gets the proxy definition type an AOT activator creates proxies of: the one of its object activator, or of its typed
            /// activator (CreateProxyInstance&lt;TProxyDefinition&gt;).
            /// </summary>
            /// <param name="activator">The activator.</param>
            /// <returns>The proxy definition type, or null for another activator.</returns>
            private static Type? GetProxyTypeDefinition(Delegate? activator)
                => activator switch
                {
                    Func<object?, object?> { Target: ObjectActivator objectActivator } => objectActivator.ProxyTypeDefinition,
                    { Target: Func<object?, object?> } typedActivator when typedActivator.GetType().IsGenericType => typedActivator.GetType().GetGenericArguments()[0],
                    _ => null,
                };

            private static Func<object?, object?>? TryCreateObjectActivator(Delegate activator)
            {
                // Dynamic methods have no declaring type and cannot be rebound through Delegate.CreateDelegate.
                // Expected binding failures must not raise first-chance exceptions during CanCreate().
                var method = activator.Method;
                if (method.DeclaringType is null)
                {
                    return null;
                }

                return Delegate.CreateDelegate(
                    typeof(Func<object?, object?>),
                    activator.Target,
                    method,
                    throwOnBindFailure: false) as Func<object?, object?>;
            }

            private static object InvokeObjectActivator(Func<object?, object?> activator, object instance)
            {
                try
                {
                    return activator(instance)!;
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(ex);
                }
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void ThrowFailure(object failure)
            {
                if (failure is Action failureThrower)
                {
                    failureThrower();

                    // A failure registration that doesn't throw still means the proxy can't be created.
                    DuckTypeException.Throw("The AOT failure registration of this proxy didn't throw an exception.");
                }

                var exceptionInfo = (ExceptionDispatchInfo)failure;
#if NET6_0_OR_GREATER
                exceptionInfo.Throw();
#else
                // WORKAROUND: https://github.com/dotnet/runtime/issues/45929
                // Fixed in the runtime by https://github.com/dotnet/runtime/pull/46636.
                //
                // Failed proxy creations are cached, so every caller for the same proxy/target type pair receives
                // the same ExceptionDispatchInfo and therefore the same Exception instance. Windows CoreCLR
                // versions before .NET 6 have a confirmed race when that instance is rethrown concurrently,
                // which can cause a crash.
                //
                // Instead, make a shallow copy of the cached exception and capture that copy immediately before every
                // throw. MemberwiseClone preserves the exact internal details, so concurrent throws no longer cause a
                // crash. Fixed in .NET 6+. AOT failure factories can register any exception type, so those are cloned
                // through object.MemberwiseClone.
                var sourceException = exceptionInfo.SourceException;
                var exceptionToThrow = sourceException is DuckTypeException duckTypeException
                                           ? duckTypeException.CloneForThrow()
                                           : (Exception)ExceptionCloner.MemberwiseCloneMethod.Invoke(sourceException, null)!;
                ExceptionDispatchInfo.Capture(exceptionToThrow).Throw();
#endif
            }

            private static Action CreateFailureThrower(object failure) => () => ThrowFailure(failure);

            [DoesNotReturn]
            private static void ThrowActivatorInvalidCast(Type proxyTypeDefinition, Type requestedActivatorType)
                => throw new InvalidCastException($"Unable to cast object of type '{typeof(CreateProxyInstance<>).FullName}[{proxyTypeDefinition}]' to type '{requestedActivatorType}'.");

            [MethodImpl(MethodImplOptions.NoInlining)]
            [return: NotNull]
            private static T CreateInstanceSlow<T>(Delegate? activator, object? instance)
            {
                if (activator is Action failureThrower)
                {
                    failureThrower();
                }

                if (activator is Func<object?, object?> objectActivator)
                {
                    // Like the cast of dynamic duck typing's typed activator (CreateProxyInstance<TProxyDefinition>) to
                    // CreateProxyInstance<T>.
                    if (objectActivator.Target is ObjectActivator { ProxyTypeDefinition: { } proxyTypeDefinition } && proxyTypeDefinition != typeof(T))
                    {
                        ThrowActivatorInvalidCast(proxyTypeDefinition, typeof(CreateProxyInstance<T>));
                    }

                    var value = objectActivator(instance);
                    if (value is null)
                    {
                        ThrowHelper.ThrowNullReferenceException("AOT duck typing activator returned null.");
                    }

                    return (T)value;
                }

                if (activator is null)
                {
                    ThrowHelper.ThrowNullReferenceException("The activator for this proxy type is null, check if the type can be created by calling 'CanCreate()'");
                }

                // The cast of the typed activator to CreateProxyInstance<T> (the message of CoreCLR's, which names both types).
                if (activator is not CreateProxyInstance<T>)
                {
                    DuckTypeAotEngine.ThrowActivatorInvalidCast(activator, typeof(CreateProxyInstance<T>));
                }

                return ((CreateProxyInstance<T>)activator)(instance);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ThrowFailureIfNeeded()
            {
                if (_failure is not null)
                {
                    ThrowFailure(_failure);
                }
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void ThrowFailureAsTargetInvocationException()
            {
                try
                {
                    ThrowFailureIfNeeded();
                }
                catch (TargetInvocationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(ex);
                }
            }

            /// <summary>
            /// An object activator of a generated (AOT) proxy, with the proxy definition type it creates.
            /// </summary>
            private sealed class ObjectActivator
            {
                private readonly Func<object?, object?> _activator;

                internal ObjectActivator(Type proxyTypeDefinition, Func<object?, object?> activator)
                {
                    ProxyTypeDefinition = proxyTypeDefinition;
                    _activator = activator;
                }

                internal Type ProxyTypeDefinition { get; }

                internal object? Create(object? instance) => _activator(instance);
            }
        }

        /// <summary>
        /// Generics Create Cache FastPath
        /// </summary>
        /// <typeparam name="T">Type of proxy definition</typeparam>
        public static class CreateCache<T>
        {
            // Because CreateTypeResult is a struct, it needs to be boxed for safe concurrent access. The entries are cleared
            // when the state that produced them changes (AOT registrations, test resets), see DuckType.InvalidateFastPaths, so
            // the fast path itself only compares the target type.
            private static FastPathEntry? _forwardFastPath;
            private static FastPathEntry? _reverseFastPath;
            private static bool _fastPathResetRegistered;

            /// <summary>
            /// Gets the type of T
            /// </summary>
            public static readonly Type Type = typeof(T);

            /// <summary>
            /// Gets the proxy type for a target type using the T proxy definition
            /// </summary>
            /// <param name="targetType">Target type</param>
            /// <returns>CreateTypeResult instance</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static CreateTypeResult GetProxy(Type targetType)
            {
                // We set a fast path for the first proxy type for a proxy definition. (It's likely to have a proxy definition just for one target type)
                var fastPath = Volatile.Read(ref _forwardFastPath);
                if (fastPath is not null && fastPath.Result.TargetType == targetType)
                {
                    return fastPath.Result;
                }

                // Read the version before computing the result: if an invalidation happens meanwhile, the result may already be
                // stale, and it isn't stored. The result is a local stored by a call: returning a call's result directly makes the
                // JIT copy the struct on every fast path hit too.
                var version = Volatile.Read(ref _fastPathVersion);
                var result = GetOrCreateProxyType(Type, targetType);
                if (Volatile.Read(ref _forwardFastPath) is null)
                {
                    // By reference: the struct isn't copied at every call site the method is inlined in.
                    StoreForwardFastPath(in result, version);
                }

                return result;
            }

            /// <summary>
            /// Create a new instance of a proxy type for a target instance using the T proxy definition
            /// </summary>
            /// <param name="instance">Object instance</param>
            /// <returns>Proxy instance</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [return: NotNullIfNotNull("instance")]
            public static T? Create(object? instance)
            {
                if (instance is null)
                {
                    return default;
                }

                return instance is T tInst ? tInst : GetProxy(instance.GetType()).CreateInstance<T>(instance);
            }

            /// <summary>
            /// Create a new instance of a proxy type for a target instance using the T proxy definition
            /// </summary>
            /// <typeparam name="TOriginal">The original instance's type </typeparam>
            /// <param name="instance">Object instance</param>
            /// <returns>Proxy instance</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [return: NotNullIfNotNull("instance")]
            public static T? CreateFrom<TOriginal>(TOriginal instance)
            {
                if (instance is null)
                {
                    return default;
                }

                return instance is T tInst ? tInst : GetProxy(typeof(TOriginal)).CreateInstance<T, TOriginal>(instance);
            }

            /// <summary>
            /// Get if the proxy instance can be created
            /// </summary>
            /// <param name="instance">Object instance</param>
            /// <returns>true if a proxy can be created; otherwise, false.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool CanCreate(object? instance)
            {
                if (instance is null)
                {
                    return false;
                }

                return instance is T || GetProxy(instance.GetType()).CanCreate();
            }

            /// <summary>
            /// Create a reverse proxy type for a target instance using the T proxy definition
            /// </summary>
            /// <param name="instance">Object instance</param>
            /// <returns>Proxy instance</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [return: NotNullIfNotNull("instance")]
            public static T? CreateReverse(object? instance)
            {
                if (instance is null)
                {
                    return default;
                }

                // Same rule as DuckType.TryUnwrapForwardProxy, using the generic type check.
                if (instance is IDuckType { Instance: T original })
                {
                    return original;
                }

                return GetReverseProxy(instance.GetType()).CreateInstance<T>(instance);
            }

            /// <summary>
            /// Gets the proxy type for a target type using the T proxy definition
            /// </summary>
            /// <param name="targetType">Target type</param>
            /// <returns>CreateTypeResult instance</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static CreateTypeResult GetReverseProxy(Type targetType)
            {
                // We set a fast path for the first proxy type for a proxy definition. (It's likely to have a proxy definition just for one target type)
                var fastPath = Volatile.Read(ref _reverseFastPath);
                if (fastPath is not null && fastPath.Result.TargetType == targetType)
                {
                    return fastPath.Result;
                }

                // Same shape as GetProxy.
                var version = Volatile.Read(ref _fastPathVersion);
                var result = GetOrCreateReverseProxyType(Type, targetType);
                if (Volatile.Read(ref _reverseFastPath) is null)
                {
                    StoreReverseFastPath(in result, version);
                }

                return result;
            }

            // Keep the first target type as the fast path: a proxy definition is likely used with a single target type.
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void StoreForwardFastPath(in CreateTypeResult result, int version)
                => StoreFastPath(ref _forwardFastPath, ref _fastPathResetRegistered, ResetFastPaths, in result, version);

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void StoreReverseFastPath(in CreateTypeResult result, int version)
                => StoreFastPath(ref _reverseFastPath, ref _fastPathResetRegistered, ResetFastPaths, in result, version);

            private static void ResetFastPaths()
            {
                Volatile.Write(ref _forwardFastPath, null);
                Volatile.Write(ref _reverseFastPath, null);
            }
        }
    }
}
