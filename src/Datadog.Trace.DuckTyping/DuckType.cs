using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Create proxy instance delegate
    /// </summary>
    /// <param name="instance">Object instance</param>
    /// <returns>Proxy instance</returns>
    public delegate IDuckType CreateProxyInstance(object instance);

    /// <summary>
    /// Create struct proxy instance delegate
    /// </summary>
    /// <typeparam name="T">Type of struct</typeparam>
    /// <param name="instance">Object instance</param>
    /// <returns>Proxy instance</returns>
    public delegate T CreateProxyInstance<T>(object instance);

    /// <summary>
    /// Duck Type
    /// </summary>
    public static partial class DuckType
    {
        /// <summary>
        /// Create duck type proxy using a base type
        /// </summary>
        /// <param name="instance">Instance object</param>
        /// <typeparam name="T">Duck type</typeparam>
        /// <returns>Duck type proxy</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Create<T>(object instance)
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
        public static IDuckType Create(Type proxyType, object instance)
        {
            // Validate arguments
            EnsureArguments(proxyType, instance);

            // Create Type
            CreateTypeResult result = GetOrCreateProxyType(proxyType, instance.GetType());

            // Create instance
            return result.CreateInstance<IDuckType>(instance);
        }

        /// <summary>
        /// Gets or create a new proxy type for ducktyping
        /// </summary>
        /// <param name="proxyType">ProxyType interface</param>
        /// <param name="targetType">Target type</param>
        /// <returns>CreateTypeResult instance</returns>
        public static CreateTypeResult GetOrCreateProxyType(Type proxyType, Type targetType)
        {
            TypesTuple key = new TypesTuple(proxyType, targetType);

            if (DuckTypeCache.TryGetValue(key, out CreateTypeResult proxyTypeResult))
            {
                return proxyTypeResult;
            }

            lock (DuckTypeCache)
            {
                if (!DuckTypeCache.TryGetValue(key, out proxyTypeResult))
                {
                    proxyTypeResult = CreateProxyType(proxyType, targetType);
                    DuckTypeCache[key] = proxyTypeResult;
                }

                return proxyTypeResult;
            }
        }

        private static CreateTypeResult CreateProxyType(Type proxyDefinitionType, Type targetType)
        {
            try
            {
                // Define parent type, interface types
                Type parentType;
                TypeAttributes typeAttributes;
                Type[] interfaceTypes;
                if (proxyDefinitionType.IsInterface || proxyDefinitionType.IsValueType)
                {
                    // If the proxy type definition is an interface we create an struct proxy
                    // If the proxy type definition is an struct then we use that struct to copy the values from the target type
                    parentType = typeof(ValueType);
                    typeAttributes = TypeAttributes.Public | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.Serializable;
                    if (proxyDefinitionType.IsInterface)
                    {
                        interfaceTypes = new[] { proxyDefinitionType, typeof(IDuckType) };
                    }
                    else
                    {
                        interfaceTypes = new[] { typeof(IDuckType) };
                    }
                }
                else
                {
                    // If the proxy type definition is a class then we create a class proxy
                    parentType = proxyDefinitionType;
                    typeAttributes = TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout | TypeAttributes.Sealed;
                    interfaceTypes = new[] { typeof(IDuckType) };
                }

                // Ensures the module builder
                if (_moduleBuilder is null)
                {
                    lock (_locker)
                    {
                        if (_moduleBuilder is null)
                        {
                            AssemblyName aName = new AssemblyName("DuckTypeAssembly");
                            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);
                            _moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
                        }
                    }
                }

                // Create a valid type name that can be used as a member of a class. (BenchmarkDotNet fails if is an invalid name)
                string proxyTypeName = $"{proxyDefinitionType.FullName.Replace(".", "_").Replace("+", "__")}___{targetType.FullName.Replace(".", "_").Replace("+", "__")}";

                // Create Type
                TypeBuilder proxyTypeBuilder = _moduleBuilder.DefineType(
                    proxyTypeName,
                    typeAttributes,
                    parentType,
                    interfaceTypes);

                // Create IDuckType and IDuckTypeSetter implementations
                FieldInfo instanceField = CreateIDuckTypeImplementation(proxyTypeBuilder, targetType);

                // Define .ctor to store the instance field
                ConstructorBuilder ctorBuilder = proxyTypeBuilder.DefineConstructor(
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                    CallingConventions.Standard,
                    new[] { targetType });
                ILGenerator ctorIL = ctorBuilder.GetILGenerator();
                ctorIL.Emit(OpCodes.Ldarg_0);
                ctorIL.Emit(OpCodes.Ldarg_1);
                ctorIL.Emit(OpCodes.Stfld, instanceField);
                ctorIL.Emit(OpCodes.Ret);

                if (proxyDefinitionType.IsValueType)
                {
                    // Create Fields and Properties from the struct information
                    CreatePropertiesFromStruct(proxyTypeBuilder, proxyDefinitionType, targetType, instanceField);

                    // Create Type
                    Type proxyType = proxyTypeBuilder.CreateTypeInfo().AsType();
                    return new CreateTypeResult(proxyType, targetType, null, CreateStructCopyMethod(proxyDefinitionType, proxyType, targetType), null);
                }
                else
                {
                    // Create Fields and Properties
                    CreateProperties(proxyTypeBuilder, proxyDefinitionType, targetType, instanceField);

                    // Create Methods
                    CreateMethods(proxyTypeBuilder, proxyDefinitionType, targetType, instanceField);

                    // Create Type
                    Type proxyType = proxyTypeBuilder.CreateTypeInfo().AsType();
                    return new CreateTypeResult(proxyType, targetType, GetCreateProxyInstanceDelegate(proxyType, targetType), null, null);
                }
            }
            catch (Exception ex)
            {
                return new CreateTypeResult(null, targetType, null, null, ExceptionDispatchInfo.Capture(ex));
            }
        }

        private static FieldInfo CreateIDuckTypeImplementation(TypeBuilder proxyTypeBuilder, Type targetType)
        {
            Type instanceType = targetType;
            if (!targetType.IsPublic && !targetType.IsNestedPublic)
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

            return instanceField;
        }

        private static List<PropertyInfo> GetProperties(Type proxyDefinitionType)
        {
            List<PropertyInfo> selectedProperties = new List<PropertyInfo>(proxyDefinitionType.IsInterface ? proxyDefinitionType.GetProperties() : GetBaseProperties(proxyDefinitionType));
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

            return selectedProperties;

            static IEnumerable<PropertyInfo> GetBaseProperties(Type baseType)
            {
                foreach (PropertyInfo prop in baseType.GetProperties())
                {
                    if (prop.DeclaringType == typeof(DuckType))
                    {
                        continue;
                    }

                    if (prop.CanRead && (prop.GetMethod.IsAbstract || prop.GetMethod.IsVirtual))
                    {
                        yield return prop;
                    }
                    else if (prop.CanWrite && (prop.SetMethod.IsAbstract || prop.SetMethod.IsVirtual))
                    {
                        yield return prop;
                    }
                }
            }
        }

        private static void CreateProperties(TypeBuilder proxyTypeBuilder, Type proxyDefinitionType, Type targetType, FieldInfo instanceField)
        {
            // Gets all properties to be implemented
            List<PropertyInfo> proxyTypeProperties = GetProperties(proxyDefinitionType);

            foreach (PropertyInfo proxyProperty in proxyTypeProperties)
            {
                PropertyBuilder propertyBuilder = null;

                // If the property is abstract or interface we make sure that we have the property defined in the new class
                if ((proxyProperty.CanRead && proxyProperty.GetMethod.IsAbstract) || (proxyProperty.CanWrite && proxyProperty.SetMethod.IsAbstract))
                {
                    propertyBuilder = proxyTypeBuilder.DefineProperty(proxyProperty.Name, PropertyAttributes.None, proxyProperty.PropertyType, null);
                }

                DuckAttribute duckAttribute = proxyProperty.GetCustomAttribute<DuckAttribute>(true) ?? new DuckAttribute();
                duckAttribute.Name ??= proxyProperty.Name;

                switch (duckAttribute.Kind)
                {
                    case DuckKind.Property:
                        PropertyInfo targetProperty = null;
                        try
                        {
                            targetProperty = targetType.GetProperty(duckAttribute.Name, DefaultFlags);
                        }
                        catch
                        {
                            // This will run only when multiple indexers are defined in a class, that way we can end up with multiple properties with the same name.
                            // In this case we make sure we select the indexer we want
                            targetProperty = targetType.GetProperty(duckAttribute.Name, proxyProperty.PropertyType, proxyProperty.GetIndexParameters().Select(i => i.ParameterType).ToArray());
                        }

                        if (targetProperty is null)
                        {
                            break;
                        }

                        propertyBuilder ??= proxyTypeBuilder.DefineProperty(proxyProperty.Name, PropertyAttributes.None, proxyProperty.PropertyType, null);

                        if (proxyProperty.CanRead)
                        {
                            // Check if the target property can be read
                            if (!targetProperty.CanRead)
                            {
                                DuckTypePropertyCantBeReadException.Throw(targetProperty);
                            }

                            propertyBuilder.SetGetMethod(GetPropertyGetMethod(proxyTypeBuilder, targetType, proxyProperty, targetProperty, instanceField));
                        }

                        if (proxyProperty.CanWrite)
                        {
                            // Check if the target property can be written
                            if (!targetProperty.CanWrite)
                            {
                                DuckTypePropertyCantBeWrittenException.Throw(targetProperty);
                            }

                            // Check if the target property declaring type is an struct (structs modification is not supported)
                            if (targetProperty.DeclaringType.IsValueType)
                            {
                                DuckTypeStructMembersCannotBeChangedException.Throw(targetProperty.DeclaringType);
                            }

                            propertyBuilder.SetSetMethod(GetPropertySetMethod(proxyTypeBuilder, targetType, proxyProperty, targetProperty, instanceField));
                        }

                        break;

                    case DuckKind.Field:
                        FieldInfo targetField = targetType.GetField(duckAttribute.Name, DefaultFlags);
                        if (targetField is null)
                        {
                            break;
                        }

                        propertyBuilder ??= proxyTypeBuilder.DefineProperty(proxyProperty.Name, PropertyAttributes.None, proxyProperty.PropertyType, null);

                        if (proxyProperty.CanRead)
                        {
                            propertyBuilder.SetGetMethod(GetFieldGetMethod(proxyTypeBuilder, targetType, proxyProperty, targetField, instanceField));
                        }

                        if (proxyProperty.CanWrite)
                        {
                            // Check if the target field is marked as InitOnly (readonly) and throw an exception in that case
                            if ((targetField.Attributes & FieldAttributes.InitOnly) != 0)
                            {
                                DuckTypeFieldIsReadonlyException.Throw(targetField);
                            }

                            // Check if the target field declaring type is an struct (structs modification is not supported)
                            if (targetField.DeclaringType.IsValueType)
                            {
                                DuckTypeStructMembersCannotBeChangedException.Throw(targetField.DeclaringType);
                            }

                            propertyBuilder.SetSetMethod(GetFieldSetMethod(proxyTypeBuilder, targetType, proxyProperty, targetField, instanceField));
                        }

                        break;
                }

                if (propertyBuilder is null)
                {
                    continue;
                }

                if (proxyProperty.CanRead && propertyBuilder.GetMethod is null)
                {
                    DuckTypePropertyOrFieldNotFoundException.Throw(proxyProperty.Name, duckAttribute.Name);
                }

                if (proxyProperty.CanWrite && propertyBuilder.SetMethod is null)
                {
                    DuckTypePropertyOrFieldNotFoundException.Throw(proxyProperty.Name, duckAttribute.Name);
                }
            }
        }

        private static void CreatePropertiesFromStruct(TypeBuilder proxyTypeBuilder, Type proxyDefinitionType, Type targetType, FieldInfo instanceField)
        {
            // Gets all fields to be copied
            foreach (FieldInfo proxyFieldInfo in proxyDefinitionType.GetFields())
            {
                PropertyBuilder propertyBuilder = null;

                DuckAttribute duckAttribute = proxyFieldInfo.GetCustomAttribute<DuckAttribute>(true) ?? new DuckAttribute();
                duckAttribute.Name ??= proxyFieldInfo.Name;

                switch (duckAttribute.Kind)
                {
                    case DuckKind.Property:
                        PropertyInfo targetProperty = targetType.GetProperty(duckAttribute.Name, DefaultFlags);
                        if (targetProperty is null)
                        {
                            break;
                        }

                        // Check if the target property can be read
                        if (!targetProperty.CanRead)
                        {
                            DuckTypePropertyCantBeReadException.Throw(targetProperty);
                        }

                        propertyBuilder = proxyTypeBuilder.DefineProperty(proxyFieldInfo.Name, PropertyAttributes.None, proxyFieldInfo.FieldType, null);
                        propertyBuilder.SetGetMethod(GetPropertyGetMethod(proxyTypeBuilder, targetType, proxyFieldInfo, targetProperty, instanceField));
                        break;

                    case DuckKind.Field:
                        FieldInfo targetField = targetType.GetField(duckAttribute.Name, DefaultFlags);
                        if (targetField is null)
                        {
                            break;
                        }

                        propertyBuilder = proxyTypeBuilder.DefineProperty(proxyFieldInfo.Name, PropertyAttributes.None, proxyFieldInfo.FieldType, null);
                        propertyBuilder.SetGetMethod(GetFieldGetMethod(proxyTypeBuilder, targetType, proxyFieldInfo, targetField, instanceField));
                        break;
                }

                if (propertyBuilder is null)
                {
                    DuckTypePropertyOrFieldNotFoundException.Throw(proxyFieldInfo.Name, duckAttribute.Name);
                }
            }
        }

        private static CreateProxyInstance GetCreateProxyInstanceDelegate(Type proxyType, Type targetType)
        {
            ConstructorInfo ctor = proxyType.GetConstructors()[0];

            DynamicMethod createProxyMethod = new DynamicMethod(
                $"CreateProxyInstance<{proxyType.Name}>",
                typeof(IDuckType),
                new[] { typeof(object) },
                typeof(DuckType).Module,
                true);
            ILGenerator il = createProxyMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (targetType.IsPublic || targetType.IsNestedPublic)
            {
                il.Emit(OpCodes.Castclass, targetType);
            }

            il.Emit(OpCodes.Newobj, ctor);

            if (proxyType.IsValueType)
            {
                il.Emit(OpCodes.Box, proxyType);
            }

            il.Emit(OpCodes.Ret);
            return (CreateProxyInstance)createProxyMethod.CreateDelegate(typeof(CreateProxyInstance));
        }

        private static Delegate CreateStructCopyMethod(Type proxyDefinitionType, Type proxyType, Type targetType)
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
            if (targetType.IsPublic || targetType.IsNestedPublic)
            {
                il.Emit(OpCodes.Castclass, targetType);
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

                PropertyInfo prop = proxyType.GetProperty(finfo.Name);
                il.Emit(OpCodes.Ldloca_S, structLocal.LocalIndex);
                il.Emit(OpCodes.Ldloca_S, proxyLocal.LocalIndex);
                il.EmitCall(OpCodes.Call, prop.GetMethod, null);
                il.Emit(OpCodes.Stfld, finfo);
            }

            // Return
            ILHelpers.WriteLoadLocal(structLocal.LocalIndex, il);
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
            /// Proxy definition type
            /// </summary>

            /// <summary>
            /// Target type
            /// </summary>
            public readonly Type TargetType;

            private readonly Type _proxyType;
            private readonly ExceptionDispatchInfo _exceptionInfo;
            private readonly CreateProxyInstance _activator;
            private readonly Delegate _structActivator;

            /// <summary>
            /// Initializes a new instance of the <see cref="CreateTypeResult"/> struct.
            /// </summary>
            /// <param name="proxyType">Proxy type</param>
            /// <param name="targetType">Target type</param>
            /// <param name="activator">Proxy activator</param>
            /// <param name="structActivator">Struct activator</param>
            /// <param name="exceptionInfo">Exception dispatch info instance</param>
            internal CreateTypeResult(Type proxyType, Type targetType, CreateProxyInstance activator, Delegate structActivator, ExceptionDispatchInfo exceptionInfo)
            {
                _proxyType = proxyType;
                TargetType = targetType;
                _activator = activator;
                _structActivator = structActivator;
                _exceptionInfo = exceptionInfo;
                Success = proxyType != null && exceptionInfo == null;
            }

            /// <summary>
            /// Gets the created ProxyType
            /// </summary>
            public Type ProxyType
            {
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
            public T CreateInstance<T>(object instance)
            {
                _exceptionInfo?.Throw();
                if (_activator is null)
                {
                    return ((CreateProxyInstance<T>)_structActivator)(instance);
                }

                return (T)_activator(instance);
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

                CreateTypeResult result = GetProxySlow(targetType);

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
            public static T Create(object instance)
            {
                if (instance is null)
                {
                    return default;
                }

                return GetProxy(instance.GetType()).CreateInstance<T>(instance);
            }

            private static CreateTypeResult GetProxySlow(Type targetType)
            {
                Type proxyTypeDefinition = typeof(T);
                if (!proxyTypeDefinition.IsValueType && !proxyTypeDefinition.IsPublic && !proxyTypeDefinition.IsNestedPublic)
                {
                    DuckTypeTypeIsNotPublicException.Throw(proxyTypeDefinition, nameof(proxyTypeDefinition));
                }

                return GetOrCreateProxyType(proxyTypeDefinition, targetType);
            }
        }
    }
}
