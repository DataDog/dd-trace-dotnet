// <copyright file="DuckType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
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
                        CreatePropertiesFromStruct(proxyTypeBuilder, proxyDefinitionType, targetType, instanceField);

                        if (dryRun)
                        {
                            // Dry run
                            return new CreateTypeResult(proxyDefinitionType, null, targetType, null, null);
                        }

                        // Create Type
                        Type proxyType = proxyTypeBuilder!.CreateTypeInfo()!.AsType();
                        return new CreateTypeResult(proxyDefinitionType, proxyType, targetType, CreateStructCopyMethod(moduleBuilder, proxyDefinitionType, proxyType, targetType), null);
                    }
                    else
                    {
                        // Create Fields and Properties
                        CreateProperties(proxyTypeBuilder, proxyDefinitionType, targetType, instanceField);

                        // Create Methods
                        CreateMethods(proxyTypeBuilder, proxyDefinitionType, targetType, instanceField);

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
                    try
                    {
                        DuckTypeException.Throw($"Error creating duck type for type: '{targetType}' using proxy: '{proxyDefinitionType}'", ex);
                        return default;
                    }
                    catch (Exception ex2)
                    {
                        return new CreateTypeResult(proxyDefinitionType, null, targetType, null, ExceptionDispatchInfo.Capture(ex2));
                    }
                }
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
                        DuckTypeReverseProxyBaseIsStructException.Throw(typeToDelegateTo);
                    }

                    // The "delegation" type can't be an interface for reverse proxy, as
                    // it needs to contain the implementations
                    if (typeToDelegateTo.IsInterface || typeToDelegateTo.IsAbstract)
                    {
                        DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException.Throw(typeToDeriveFrom);
                    }

                    ModuleBuilder? moduleBuilder = null;
                    TypeBuilder? proxyTypeBuilder = null;
                    FieldInfo? instanceField = null;

                    if (!dryRun)
                    {
                        moduleBuilder = CreateTypeAndModuleBuilder(typeToDeriveFrom, typeToDelegateTo, out proxyTypeBuilder, out instanceField);
                    }

                    // Create Fields and Properties
                    CreateReverseProxyProperties(proxyTypeBuilder, typeToDeriveFrom, typeToDelegateTo, instanceField);

                    // Create Methods
                    CreateReverseProxyMethods(proxyTypeBuilder, typeToDeriveFrom, typeToDelegateTo, instanceField);

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
                    try
                    {
                        DuckTypeException.Throw($"Error creating duck type for type: '{typeToDelegateTo}' using proxy: '{typeToDeriveFrom}'", ex);
                        return default;
                    }
                    catch (Exception ex2)
                    {
                        return new CreateTypeResult(typeToDeriveFrom, null, typeToDelegateTo, null, ExceptionDispatchInfo.Capture(ex2));
                    }
                }
            }
        }

        private static ModuleBuilder CreateTypeAndModuleBuilder(Type typeToDeriveFrom, Type typeToDelegateTo, out TypeBuilder proxyTypeBuilder, out FieldInfo instanceField)
        {
            // Define parent type, interface types
            Type parentType;
            TypeAttributes typeAttributes;
            Type[] interfaceTypes;

            if (typeToDeriveFrom.IsInterface || typeToDeriveFrom.IsValueType)
            {
                // If the proxy type definition is an interface we create an struct proxy
                // If the proxy type definition is an struct then we use that struct to copy the values from the target type
                parentType = typeof(ValueType);
                typeAttributes = TypeAttributes.Public | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.Serializable;
                if (typeToDeriveFrom.IsInterface)
                {
                    interfaceTypes = new[] { typeToDeriveFrom, typeof(IDuckType) };
                }
                else
                {
                    interfaceTypes = new[] { typeof(IDuckType) };
                }
            }
            else
            {
                // If the proxy type definition is a class then we create a class proxy
                parentType = typeToDeriveFrom;
                typeAttributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout | TypeAttributes.Sealed;
                interfaceTypes = new[] { typeof(IDuckType) };
            }

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
                byte[] pbToken = asmName.GetPublicKeyToken() ?? Array.Empty<byte>();
                assembly += "__" + BitConverter.ToString(pbToken).Replace("-", string.Empty);
                assembly = assembly.Replace(".", "_").Replace("+", "__");
            }

            // Create a valid type name that can be used as a member of a class. (BenchmarkDotNet fails if is an invalid name)
            string proxyTypeName = $"{assembly}.{typeToDelegateTo.FullName?.Replace(".", "_").Replace("+", "__")}.{typeToDeriveFrom.FullName?.Replace(".", "_").Replace("+", "__")}_{++_typeCount}";

            // Create Type
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
        }

        private static FieldInfo CreateIDuckTypeImplementation(TypeBuilder proxyTypeBuilder, Type targetType)
        {
            Type instanceType = targetType;
            if (!UseDirectAccessTo(proxyTypeBuilder, targetType))
            {
                instanceType = typeof(object);
            }

            FieldBuilder instanceField = proxyTypeBuilder.DefineField("_currentInstance", instanceType, FieldAttributes.Private | FieldAttributes.InitOnly);

            PropertyBuilder propInstance = proxyTypeBuilder.DefineProperty("Instance", PropertyAttributes.None, typeof(object), null);
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

            PropertyBuilder propType = proxyTypeBuilder.DefineProperty("Type", PropertyAttributes.None, typeof(Type), null);
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

            var toStringTargetType = targetType.GetMethod("ToString", Type.EmptyTypes);
            if (toStringTargetType is not null)
            {
                MethodBuilder toStringMethod = proxyTypeBuilder.DefineMethod("ToString", toStringTargetType.Attributes, typeof(string), Type.EmptyTypes);
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
        private static void CreateProperties(TypeBuilder? proxyTypeBuilder, Type proxyDefinitionType, Type targetType, FieldInfo? instanceField)
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
                    DuckTypeIncorrectReversePropertyUsageException.Throw(proxyProperty);
                }

                PropertyBuilder? propertyBuilder = null;

                DuckAttribute duckAttribute = proxyProperty.GetCustomAttribute<DuckAttribute>(true) ?? new DuckAttribute();
                duckAttribute.Name ??= proxyProperty.Name;

                switch (duckAttribute.Kind)
                {
                    case DuckKind.Property:
                        PropertyInfo? targetProperty = null;
                        try
                        {
                            targetProperty = targetType.GetProperty(duckAttribute.Name, duckAttribute.BindingFlags);
                        }
                        catch
                        {
                            // This will run only when multiple indexers are defined in a class, that way we can end up with multiple properties with the same name.
                            // In this case we make sure we select the indexer we want
                            targetProperty = targetType.GetProperty(duckAttribute.Name, proxyProperty.PropertyType, proxyProperty.GetIndexParameters().Select(i => i.ParameterType).ToArray());
                        }

                        if (targetProperty is null)
                        {
                            if (proxyProperty.CanRead && proxyProperty.GetMethod is not null)
                            {
                                var getMethod = proxyProperty.GetMethod;
                                if (getMethod.IsAbstract || getMethod.IsVirtual)
                                {
                                    DuckTypePropertyOrFieldNotFoundException.Throw(proxyProperty.Name, duckAttribute.Name, targetType);
                                }
                            }

                            if (proxyProperty.CanWrite && proxyProperty.SetMethod is not null)
                            {
                                var setMethod = proxyProperty.SetMethod;
                                if (setMethod.IsAbstract || setMethod.IsVirtual)
                                {
                                    DuckTypePropertyOrFieldNotFoundException.Throw(proxyProperty.Name, duckAttribute.Name, targetType);
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
                                DuckTypePropertyCantBeReadException.Throw(targetProperty);
                            }

                            MethodBuilder? getMethodBuilder = GetPropertyGetMethod(
                                proxyTypeBuilder,
                                targetType: targetType,
                                proxyMember: proxyProperty,
                                targetProperty: targetProperty,
                                instanceField: instanceField,
                                duckCastInnerToOuterFunc: MethodIlHelper.AddIlToDuckChain,
                                needsDuckChaining: NeedsDuckChaining);

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
                                DuckTypePropertyCantBeWrittenException.Throw(targetProperty);
                            }

                            // Check if the target property declaring type is an struct (structs modification is not supported)
                            if (targetProperty.DeclaringType?.IsValueType == true)
                            {
                                DuckTypeStructMembersCannotBeChangedException.Throw(targetProperty.DeclaringType);
                            }

                            MethodBuilder? setMethodBuilder = GetPropertySetMethod(
                                proxyTypeBuilder,
                                targetType: targetType,
                                proxyMember: proxyProperty,
                                targetProperty: targetProperty,
                                instanceField: instanceField,
                                duckCastOuterToInner: MethodIlHelper.AddIlToExtractDuckType,
                                needsDuckChaining: NeedsDuckChaining);

                            if (setMethodBuilder is not null)
                            {
                                propertyBuilder?.SetSetMethod(setMethodBuilder);
                            }
                        }

                        break;

                    case DuckKind.Field:
                        FieldInfo? targetField = targetType.GetField(duckAttribute.Name, duckAttribute.BindingFlags);
                        if (targetField is null)
                        {
                            DuckTypePropertyOrFieldNotFoundException.Throw(proxyProperty.Name, duckAttribute.Name, targetType);
                            continue;
                        }

                        propertyBuilder = proxyTypeBuilder?.DefineProperty(proxyProperty.Name, PropertyAttributes.None, proxyProperty.PropertyType, null);

                        if (proxyProperty.CanRead)
                        {
                            MethodBuilder? getMethodBuilder = GetFieldGetMethod(proxyTypeBuilder, targetType, proxyProperty, targetField, instanceField);
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
                                DuckTypeFieldIsReadonlyException.Throw(targetField);
                            }

                            // Check if the target field declaring type is an struct (structs modification is not supported)
                            if (targetField.DeclaringType?.IsValueType == true)
                            {
                                DuckTypeStructMembersCannotBeChangedException.Throw(targetField.DeclaringType);
                            }

                            MethodBuilder? setMethodBuilder = GetFieldSetMethod(proxyTypeBuilder, targetType, proxyProperty, targetField, instanceField);
                            if (setMethodBuilder is not null)
                            {
                                propertyBuilder?.SetSetMethod(setMethodBuilder);
                            }
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Create properties in <paramref name="proxyTypeBuilder"/>
        /// </summary>
        /// <param name="proxyTypeBuilder">The type builder for the new proxy</param>
        /// <param name="typeToDeriveFrom">The type we're inheriting from/implementing</param>
        /// <param name="typeToDelegateTo">The type we're delegating the implementation too</param>
        /// <param name="instanceField">The field for accessing the instance of the <paramref name="typeToDelegateTo"/></param>
        private static void CreateReverseProxyProperties(TypeBuilder? proxyTypeBuilder, Type typeToDeriveFrom, Type typeToDelegateTo, FieldInfo? instanceField)
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
                    DuckTypeReverseProxyPropertyCannotBeAbstractException.Throw(implementationProperty);
                }

                PropertyInfo? overriddenProperty = null;
                try
                {
                    overriddenProperty = typeToDeriveFrom.GetProperty(duckAttribute.Name, duckAttribute.BindingFlags);
                }
                catch
                {
                    // This will run only when multiple indexers are defined in a class, that way we can end up with multiple properties with the same name.
                    // In this case we make sure we select the indexer we want
                    overriddenProperty = typeToDeriveFrom.GetProperty(duckAttribute.Name, implementationProperty.PropertyType, implementationProperty.GetIndexParameters().Select(i => i.ParameterType).ToArray());
                }

                if (overriddenProperty is null)
                {
                    DuckTypePropertyOrFieldNotFoundException.Throw(implementationProperty.Name, duckAttribute.Name, typeToDeriveFrom);
                    continue;
                }

                propertyBuilder = proxyTypeBuilder?.DefineProperty(implementationProperty.Name, PropertyAttributes.None, implementationProperty.PropertyType, null);

                if (implementationProperty.CanRead)
                {
                    // Check if the target property can be read
                    if (!overriddenProperty.CanRead)
                    {
                        DuckTypePropertyCantBeReadException.Throw(overriddenProperty);
                    }

                    MethodBuilder? getMethodBuilder = GetPropertyGetMethod(
                        proxyTypeBuilder,
                        targetType: typeToDeriveFrom,
                        proxyMember: overriddenProperty,
                        targetProperty: implementationProperty,
                        instanceField: instanceField,
                        duckCastInnerToOuterFunc: MethodIlHelper.AddIlToExtractDuckType,
                        needsDuckChaining: MethodIlHelper.NeedsDuckChainingReverse);

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
                        DuckTypePropertyCantBeWrittenException.Throw(overriddenProperty);
                    }

                    // Check if the target property declaring type is an struct (structs modification is not supported)
                    if (overriddenProperty.DeclaringType?.IsValueType == true)
                    {
                        DuckTypeStructMembersCannotBeChangedException.Throw(overriddenProperty.DeclaringType);
                    }

                    MethodBuilder? setMethodBuilder = GetPropertySetMethod(
                        proxyTypeBuilder,
                        targetType: typeToDeriveFrom,
                        proxyMember: overriddenProperty,
                        targetProperty: implementationProperty,
                        instanceField: instanceField,
                        duckCastOuterToInner: MethodIlHelper.AddIlToDuckChain,
                        needsDuckChaining: MethodIlHelper.NeedsDuckChainingReverse);

                    if (setMethodBuilder is not null)
                    {
                        propertyBuilder?.SetSetMethod(setMethodBuilder);
                    }
                }

                propertiesThatShouldBeImplemented.RemoveAll(prop => duckAttribute.Name == prop.Name);
            }

            if (propertiesThatShouldBeImplemented.Count > 0)
            {
                DuckTypeReverseProxyMissingPropertyImplementationException.Throw(propertiesThatShouldBeImplemented);
            }
        }

        /// <summary>
        /// Create properties in <paramref name="proxyTypeBuilder"/>
        /// </summary>
        /// <param name="proxyTypeBuilder">The type builder for the new proxy</param>
        /// <param name="proxyDefinitionType">The custom type we defined</param>
        /// <param name="targetType">The original type we are proxying</param>
        /// <param name="instanceField">The field for accessing the instance of the <paramref name="targetType"/></param>
        private static void CreatePropertiesFromStruct(TypeBuilder? proxyTypeBuilder, Type proxyDefinitionType, Type targetType, FieldInfo? instanceField)
        {
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

                PropertyBuilder? propertyBuilder = null;
                MethodBuilder? getMethodBuilder = null;

                DuckAttribute duckAttribute = proxyFieldInfo.GetCustomAttribute<DuckAttribute>(true) ?? new DuckAttribute();
                duckAttribute.Name ??= proxyFieldInfo.Name;

                switch (duckAttribute.Kind)
                {
                    case DuckKind.Property:
                        PropertyInfo? targetProperty = targetType.GetProperty(duckAttribute.Name, duckAttribute.BindingFlags);
                        if (targetProperty is null)
                        {
                            DuckTypePropertyOrFieldNotFoundException.Throw(proxyFieldInfo.Name, duckAttribute.Name, targetType);
                            continue;
                        }

                        // Check if the target property can be read
                        if (!targetProperty.CanRead)
                        {
                            DuckTypePropertyCantBeReadException.Throw(targetProperty);
                        }

                        propertyBuilder = proxyTypeBuilder?.DefineProperty(proxyFieldInfo.Name, PropertyAttributes.None, proxyFieldInfo.FieldType, null);

                        getMethodBuilder = GetPropertyGetMethod(
                            proxyTypeBuilder,
                            targetType: targetType,
                            proxyMember: proxyFieldInfo,
                            targetProperty: targetProperty,
                            instanceField: instanceField,
                            duckCastInnerToOuterFunc: MethodIlHelper.AddIlToDuckChain,
                            needsDuckChaining: NeedsDuckChaining);

                        if (getMethodBuilder is not null)
                        {
                            propertyBuilder?.SetGetMethod(getMethodBuilder);
                        }

                        break;

                    case DuckKind.Field:
                        FieldInfo? targetField = targetType.GetField(duckAttribute.Name, duckAttribute.BindingFlags);
                        if (targetField is null)
                        {
                            DuckTypePropertyOrFieldNotFoundException.Throw(proxyFieldInfo.Name, duckAttribute.Name, targetType);
                            continue;
                        }

                        propertyBuilder = proxyTypeBuilder?.DefineProperty(proxyFieldInfo.Name, PropertyAttributes.None, proxyFieldInfo.FieldType, null);
                        getMethodBuilder = GetFieldGetMethod(proxyTypeBuilder, targetType, proxyFieldInfo, targetField, instanceField);
                        if (getMethodBuilder is not null)
                        {
                            propertyBuilder?.SetGetMethod(getMethodBuilder);
                        }

                        break;
                }
            }
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

        private static Delegate CreateStructCopyMethod(ModuleBuilder? moduleBuilder, Type proxyDefinitionType, Type proxyType, Type targetType)
        {
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
                if (targetType.IsValueType)
                {
                    il.Emit(OpCodes.Unbox_Any, targetType);
                }
                else
                {
                    il.Emit(OpCodes.Castclass, targetType);
                }
            }

            il.Emit(OpCodes.Call, ctor);

            // Create the destination structure
            il.Emit(OpCodes.Ldloca_S, structLocal.LocalIndex);
            il.Emit(OpCodes.Initobj, proxyDefinitionType);

            // Start copy properties from the proxy to the structure
            foreach (FieldInfo finfo in proxyDefinitionType.GetFields())
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

                PropertyInfo? prop = proxyType.GetProperty(finfo.Name);
                if (prop?.GetMethod is not null)
                {
                    il.Emit(OpCodes.Ldloca_S, structLocal.LocalIndex);
                    il.Emit(OpCodes.Ldloca_S, proxyLocal.LocalIndex);
                    il.EmitCall(OpCodes.Call, prop.GetMethod, null);
                    il.Emit(OpCodes.Stfld, finfo);
                }
            }

            // Return
            il.WriteLoadLocal(structLocal.LocalIndex);
            il.Emit(OpCodes.Ret);

            Type delegateType = typeof(CreateProxyInstance<>).MakeGenericType(proxyDefinitionType);
            return createStructMethod.CreateDelegate(delegateType);
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
            private static CreateTypeResult _fastPath = default;

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
                CreateTypeResult fastPath = _fastPath;
                if (fastPath.TargetType == targetType)
                {
                    return fastPath;
                }

                CreateTypeResult result = GetOrCreateProxyType(Type, targetType);

                fastPath = _fastPath;
                if (fastPath.TargetType is null)
                {
                    _fastPath = result;
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

                return GetProxy(instance.GetType()).CreateInstance<T>(instance);
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

                return GetProxy(typeof(TOriginal)).CreateInstance<T, TOriginal>(instance);
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

                return GetProxy(instance.GetType()).CanCreate();
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
                CreateTypeResult fastPath = _fastPath;
                if (fastPath.TargetType == targetType)
                {
                    return fastPath;
                }

                CreateTypeResult result = GetOrCreateReverseProxyType(Type, targetType);

                fastPath = _fastPath;
                if (fastPath.TargetType is null)
                {
                    _fastPath = result;
                }

                return result;
            }
        }
    }
}
