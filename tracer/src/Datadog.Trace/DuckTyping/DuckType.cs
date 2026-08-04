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
            return DuckTypeCache.GetOrAdd(
                new TypesTuple(proxyType, targetType),
                key => new Lazy<CreateTypeResult>(() =>
                {
                    var dryResult = CreateProxyType(key.ProxyDefinitionType, key.TargetType, true);
                    if (dryResult.CanCreate())
                    {
                        return CreateProxyType(key.ProxyDefinitionType, key.TargetType, false);
                    }

                    return dryResult;
                }))
                .Value;
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
            return DuckTypeCache.GetOrAdd(
                new TypesTuple(typeToDeriveFrom, delegationType),
                key => new Lazy<CreateTypeResult>(() =>
                {
                    var dryResult = CreateReverseProxyType(key.ProxyDefinitionType, key.TargetType, true);
                    if (dryResult.CanCreate())
                    {
                        return CreateReverseProxyType(key.ProxyDefinitionType, key.TargetType, false);
                    }

                    return dryResult;
                }))
                .Value;
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
                            return new CreateTypeResult(proxyDefinitionType, null, targetType, null, null);
                        }

                        // Create Type
                        Type proxyType = proxyTypeBuilder!.CreateTypeInfo()!.AsType();

                        if (CreateStructCopyMethod(moduleBuilder, proxyDefinitionType, proxyType, targetType, out var structActivator) is { } copyError)
                        {
                            return Failed(copyError);
                        }

                        return new CreateTypeResult(proxyDefinitionType, proxyType, targetType, structActivator, null);
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
                            return new CreateTypeResult(proxyDefinitionType, null, targetType, null, null);
                        }

                        // Create Type
                        Type proxyType = proxyTypeBuilder!.CreateTypeInfo()!.AsType();
                        return new CreateTypeResult(proxyDefinitionType, proxyType, targetType, GetCreateProxyInstanceDelegate(moduleBuilder, proxyDefinitionType, proxyType, targetType), null);
                    }
                }
                catch (DuckTypeException ex)
                {
                    return new CreateTypeResult(proxyDefinitionType, null, targetType, null, ExceptionDispatchInfo.Capture(ex));
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
                        return new CreateTypeResult(typeToDeriveFrom, null, typeToDelegateTo, null, null);
                    }

                    // Create Type
                    Type? proxyType = proxyTypeBuilder!.CreateTypeInfo()!.AsType();
                    return new CreateTypeResult(typeToDeriveFrom, proxyType, typeToDelegateTo, GetCreateProxyInstanceDelegate(moduleBuilder, typeToDeriveFrom, proxyType, typeToDelegateTo), null);
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

            // Create a "valid" type name (doesn't always hold) that can be used as a member of a class. (BenchmarkDotNet fails if is an invalid name)
            // The name we generate here is primarily for debugging purposes (stack traces etc), so we don't try too hard
            var proxyTypeNameSuffix = $"_{(++_typeCount).ToString(CultureInfo.InvariantCulture)}";
            var proxyTypeNamePrefix = $"{assembly}.{typeToDelegateTo.FullName?.Replace(".", "_").Replace("+", "__")}.{typeToDeriveFrom.FullName?.Replace(".", "_").Replace("+", "__")}";

            // the maximum length for an assembly-qualified type name is 1024, so we need to account for that
            var maxPrefixSize = 1023 - proxyTypeNameSuffix.Length;
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
                var proxyCtor = typeToDeriveFrom.GetTypeInfo().DeclaredConstructors.Where(pCtor => pCtor.GetParameters().Length == 0).FirstOrDefault();
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
                        PropertyInfo? targetProperty = GetTargetPropertyOrIndex(targetType, duckAttribute.Name, duckAttribute.BindingFlags, proxyProperty);

                        if (duckAttribute.FallbackToBaseTypes)
                        {
                            var currentType = targetType;
                            while (targetProperty is null && currentType is { IsValueType: false, BaseType: not null } && currentType.BaseType != typeof(object))
                            {
                                currentType = currentType.BaseType;
                                targetProperty = GetTargetPropertyOrIndex(currentType, duckAttribute.Name, duckAttribute.BindingFlags, proxyProperty);
                            }
                        }

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
                                proxyMethodResult: out var getMethodBuilder,
                                duckCastInnerToOuterFunc: MethodIlHelper.AddIlToDuckChain,
                                needsDuckChaining: NeedsDuckChaining) is { } getError)
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
                        FieldInfo? targetField = GetTargetField(targetType, duckAttribute.Name, duckAttribute.BindingFlags);

                        if (duckAttribute.FallbackToBaseTypes)
                        {
                            var currentType = targetType;
                            while (targetField is null && currentType is { IsValueType: false, BaseType: not null } && currentType.BaseType != typeof(object))
                            {
                                currentType = currentType.BaseType;
                                targetField = GetTargetField(currentType, duckAttribute.Name, duckAttribute.BindingFlags);
                            }
                        }

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

                PropertyInfo? overriddenProperty = GetTargetPropertyOrIndex(typeToDeriveFrom, duckAttribute.Name, duckAttribute.BindingFlags, implementationProperty);
                if (overriddenProperty is null)
                {
                    return DuckTypePropertyOrFieldNotFoundException.Create(implementationProperty.Name, duckAttribute.Name, typeToDeriveFrom);
                }

                propertyBuilder = proxyTypeBuilder?.DefineProperty(implementationProperty.Name, PropertyAttributes.None, implementationProperty.PropertyType, null);

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
                        proxyMethodResult: out var getMethodBuilder,
                        duckCastInnerToOuterFunc: MethodIlHelper.AddIlToExtractDuckType,
                        needsDuckChaining: MethodIlHelper.NeedsDuckChainingReverse) is { } getError)
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

                propertiesThatShouldBeImplemented.RemoveAll(prop =>
                {
                    if (duckAttribute.Name.IndexOf(',') == -1)
                    {
                        return duckAttribute.Name == prop.Name;
                    }

                    foreach (var name in duckAttribute.Name.Split(','))
                    {
                        if (name == prop.Name)
                        {
                            return true;
                        }
                    }

                    return false;
                });
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
                        PropertyInfo? targetProperty = GetTargetProperty(targetType, duckAttribute.Name, duckAttribute.BindingFlags);

                        if (duckAttribute.FallbackToBaseTypes)
                        {
                            var currentType = targetType;
                            while (targetProperty is null && currentType is { IsValueType: false, BaseType: not null } && currentType.BaseType != typeof(object))
                            {
                                currentType = currentType.BaseType;
                                targetProperty = GetTargetProperty(currentType, duckAttribute.Name, duckAttribute.BindingFlags);
                            }
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
                            proxyMethodResult: out getMethodBuilder,
                            duckCastInnerToOuterFunc: MethodIlHelper.AddIlToDuckChain,
                            needsDuckChaining: NeedsDuckChaining) is { } getError)
                        {
                            return getError;
                        }

                        if (getMethodBuilder is not null)
                        {
                            propertyBuilder?.SetGetMethod(getMethodBuilder);
                        }

                        break;

                    case DuckKind.Field:
                        FieldInfo? targetField = GetTargetField(targetType, duckAttribute.Name, duckAttribute.BindingFlags);

                        if (duckAttribute.FallbackToBaseTypes)
                        {
                            var currentType = targetType;
                            while (targetField is null && currentType is { IsValueType: false, BaseType: not null } && currentType.BaseType != typeof(object))
                            {
                                currentType = currentType.BaseType;
                                targetField = GetTargetField(currentType, duckAttribute.Name, duckAttribute.BindingFlags);
                            }
                        }

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
            il.Emit(OpCodes.Ldloca_S, proxyLocal.LocalIndex);
            il.Emit(OpCodes.Ldarg_0);
            if (UseDirectAccessTo(moduleBuilder, targetType))
            {
                il.Emit(targetType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, targetType);
            }

            il.Emit(OpCodes.Call, ctor);

            // Create the destination structure
            il.Emit(OpCodes.Ldloca_S, structLocal.LocalIndex);
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
                    il.Emit(OpCodes.Ldloca_S, structLocal.LocalIndex);
                    il.Emit(OpCodes.Ldloca_S, proxyLocal.LocalIndex);
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

        private static PropertyInfo? GetTargetPropertyOrIndex(Type targetType, string propertyName, BindingFlags bindingFlags, PropertyInfo proxyPropertyInfo)
        {
            if (propertyName.IndexOf(',') == -1)
            {
                return FindPropertyOrIndex(targetType, propertyName, bindingFlags, proxyPropertyInfo);
            }

            PropertyInfo? targetProperty = null;
            foreach (var name in propertyName.Split(','))
            {
                targetProperty = FindPropertyOrIndex(targetType, name, bindingFlags, proxyPropertyInfo);

                if (targetProperty is not null)
                {
                    break;
                }
            }

            return targetProperty;

            static PropertyInfo? FindPropertyOrIndex(Type targetType, string propertyName, BindingFlags bindingFlags, PropertyInfo proxyPropertyInfo)
            {
                // Type.GetProperty(name, bindingFlags) throws AmbiguousMatchException when several properties
                // match, which happens whenever the target declares more than one indexer, so when the proxy member is an indexer,
                // ask for its exact signature first and avoid the ambiguity altogether.
                var indexParameters = proxyPropertyInfo.GetIndexParameters();
                if (indexParameters.Length > 0)
                {
                    // Matching a full signature is deterministic, so this cannot be ambiguous. Done by hand
                    // rather than with GetProperty(name, returnType, types) because that overload only searches
                    // public members, and a non-public indexer would otherwise fall through to the throwing
                    // lookup below.
                    var indexer = FindExactIndexer(targetType, propertyName, proxyPropertyInfo.PropertyType, indexParameters, bindingFlags);
                    if (indexer is not null)
                    {
                        return indexer;
                    }

                    // Fall through: the target's indexer differs from the proxy's in a way that only needs a
                    // conversion, so the plain lookup below still has to resolve it.
                }

                try
                {
                    return targetType.GetProperty(propertyName, bindingFlags);
                }
                catch
                {
                    // Several indexers are declared and none matched the proxy's exact signature above.
                    var parameterTypes = new Type[indexParameters.Length];
                    for (var i = 0; i < indexParameters.Length; i++)
                    {
                        parameterTypes[i] = indexParameters[i].ParameterType;
                    }

                    return targetType.GetProperty(propertyName, proxyPropertyInfo.PropertyType, parameterTypes);
                }
            }

            static PropertyInfo? FindExactIndexer(Type targetType, string propertyName, Type propertyType, ParameterInfo[] indexParameters, BindingFlags bindingFlags)
            {
                foreach (var candidate in targetType.GetProperties(bindingFlags))
                {
                    if (candidate.Name != propertyName || candidate.PropertyType != propertyType)
                    {
                        continue;
                    }

                    var candidateParameters = candidate.GetIndexParameters();
                    if (candidateParameters.Length != indexParameters.Length)
                    {
                        continue;
                    }

                    var matches = true;
                    for (var i = 0; i < candidateParameters.Length; i++)
                    {
                        if (candidateParameters[i].ParameterType != indexParameters[i].ParameterType)
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                    {
                        return candidate;
                    }
                }

                return null;
            }
        }

        private static PropertyInfo? GetTargetProperty(Type targetType, string propertyName, BindingFlags bindingFlags)
        {
            if (propertyName.IndexOf(',') == -1)
            {
                return targetType.GetProperty(propertyName, bindingFlags);
            }

            PropertyInfo? targetProperty = null;
            foreach (var name in propertyName.Split(','))
            {
                targetProperty = targetType.GetProperty(name, bindingFlags);
                if (targetProperty is not null)
                {
                    break;
                }
            }

            return targetProperty;
        }

        private static FieldInfo? GetTargetField(Type targetType, string fieldName, BindingFlags bindingFlags)
        {
            if (fieldName.IndexOf(',') == -1)
            {
                return targetType.GetField(fieldName, bindingFlags);
            }

            FieldInfo? targetField = null;
            foreach (var name in fieldName.Split(','))
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
            private readonly ExceptionDispatchInfo? _exceptionInfo;

            /// <summary>
            /// Initializes a new instance of the <see cref="CreateTypeResult"/> struct.
            /// </summary>
            /// <param name="proxyTypeDefinition">Proxy type definition</param>
            /// <param name="proxyType">Proxy type</param>
            /// <param name="targetType">Target type</param>
            /// <param name="activator">Proxy activator</param>
            /// <param name="exceptionInfo">Exception dispatch info instance</param>
            internal CreateTypeResult(Type proxyTypeDefinition, Type? proxyType, Type targetType, Delegate? activator, ExceptionDispatchInfo? exceptionInfo)
            {
                _activator = activator;
                _proxyType = proxyType;
                _exceptionInfo = exceptionInfo;
                TargetType = targetType;
                Success = proxyType != null && exceptionInfo == null;
                if (exceptionInfo is not null)
                {
                    MethodInfo methodInfo = typeof(CreateTypeResult).GetMethod(nameof(ThrowOnError), BindingFlags.NonPublic | BindingFlags.Instance)!;
                    _activator = methodInfo
                        .MakeGenericMethod(proxyTypeDefinition)
                        .CreateDelegate(
                        typeof(CreateProxyInstance<>).MakeGenericType(proxyTypeDefinition),
                        this);
                }
            }

            /// <summary>
            /// Gets the Proxy type
            /// </summary>
            public Type? ProxyType
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    _exceptionInfo?.Throw();
                    return _proxyType;
                }
            }

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
                if (_activator is null)
                {
                    ThrowHelper.ThrowNullReferenceException("The activator for this proxy type is null, check if the type can be created by calling 'CanCreate()'");
                }

                return ((CreateProxyInstance<T>)_activator)(instance);
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
                if (_activator is null)
                {
                    ThrowHelper.ThrowNullReferenceException("The activator for this proxy type is null, check if the type can be created by calling 'CanCreate()'");
                }

                return ((CreateProxyInstance<T>)_activator)(instance);
            }

            /// <summary>
            /// Get if the proxy instance can be created
            /// </summary>
            /// <returns>true if the proxy can be created; otherwise, false.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool CanCreate()
            {
                return _exceptionInfo == null;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal object CreateInstance(object instance)
            {
                if (_activator is null)
                {
                    ThrowHelper.ThrowNullReferenceException("The activator for this proxy type is null, check if the type can be created by calling 'CanCreate()'");
                }

                return _activator.DynamicInvoke(instance)!;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private T? ThrowOnError<T>(object? instance)
            {
                _exceptionInfo?.Throw();
                return default;
            }
        }

        /// <summary>
        /// Generics Create Cache FastPath
        /// </summary>
        /// <typeparam name="T">Type of proxy definition</typeparam>
        public static class CreateCache<T>
        {
            // Because CreateTypeResult is a struct, it needs to be boxed for safe concurrent access
            private static StrongBox<CreateTypeResult>? _fastPath;

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
                var fastPath = Volatile.Read(ref _fastPath);

                if (fastPath?.Value.TargetType == targetType)
                {
                    return fastPath.Value;
                }

                CreateTypeResult result = GetOrCreateProxyType(Type, targetType);

                _fastPath ??= new(result);

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
                var fastPath = Volatile.Read(ref _fastPath);

                if (fastPath?.Value.TargetType == targetType)
                {
                    return fastPath.Value;
                }

                CreateTypeResult result = GetOrCreateReverseProxyType(Type, targetType);

                _fastPath ??= new(result);

                return result;
            }
        }
    }
}
