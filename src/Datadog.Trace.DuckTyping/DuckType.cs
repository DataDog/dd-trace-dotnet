using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;

namespace Datadog.Trace.DuckTyping
{
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
        public static T Create<T>(object instance)
        {
            return (T)Create(typeof(T), instance);
        }

        /// <summary>
        /// Create duck type proxy using a base type
        /// </summary>
        /// <param name="proxyType">Duck type</param>
        /// <param name="instance">Instance object</param>
        /// <returns>Duck Type proxy</returns>
        public static IDuckType Create(Type proxyType, object instance)
        {
            // Validate arguments
            EnsureArguments(proxyType, instance);

            // Create Type
            CreateTypeResult result = GetOrCreateProxyType(proxyType, instance.GetType());

            // Create instance
            return (IDuckType)Activator.CreateInstance(result.ProxyType, instance);
        }

        /// <summary>
        /// Gets or create a new proxy type for ducktyping
        /// </summary>
        /// <param name="proxyType">ProxyType interface</param>
        /// <param name="targetType">Target type</param>
        /// <returns>CreateTypeResult instance</returns>
        public static CreateTypeResult GetOrCreateProxyType(Type proxyType, Type targetType)
        {
            VTuple<Type, Type> key = new VTuple<Type, Type>(proxyType, targetType);

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

        private static CreateTypeResult CreateProxyType(Type proxyType, Type targetType)
        {
            try
            {
                // Define parent type, interface types
                Type parentType;
                TypeAttributes typeAttributes;
                Type[] interfaceTypes;
                if (proxyType.IsInterface)
                {
                    // If the proxy type definition is an interface we create an struct proxy
                    parentType = typeof(ValueType);
                    typeAttributes = TypeAttributes.Public | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.Sealed | TypeAttributes.Serializable;
                    interfaceTypes = new[] { proxyType, typeof(IDuckType) };
                }
                else
                {
                    // If the proxy type definition is a class then we create a class proxy
                    parentType = proxyType;
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
                string proxyTypeName = $"{proxyType.FullName.Replace(".", "_").Replace("+", "__")}___{targetType.FullName.Replace(".", "_").Replace("+", "__")}";

                // Create Type
                TypeBuilder proxyTypeBuilder = _moduleBuilder.DefineType(
                    proxyTypeName,
                    typeAttributes,
                    parentType,
                    interfaceTypes);

                // Create IDuckType and IDuckTypeSetter implementations
                FieldInfo instanceField = CreateIDuckTypeImplementation(proxyTypeBuilder, targetType);

                // Define .ctor
                ConstructorBuilder ctorBuilder = proxyTypeBuilder.DefineConstructor(
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                    CallingConventions.Standard,
                    new[] { targetType });
                ILGenerator ctorIL = ctorBuilder.GetILGenerator();
                ctorIL.Emit(OpCodes.Ldarg_0);
                ctorIL.Emit(OpCodes.Ldarg_1);
                ctorIL.Emit(OpCodes.Stfld, instanceField);
                ctorIL.Emit(OpCodes.Ret);

                // Create Members
                CreateProperties(proxyTypeBuilder, proxyType, targetType, instanceField);
                CreateMethods(proxyTypeBuilder, proxyType, targetType, instanceField);

                // Create Type
                return new CreateTypeResult(proxyTypeBuilder.CreateTypeInfo().AsType(), null);
            }
            catch (Exception ex)
            {
                return new CreateTypeResult(null, ExceptionDispatchInfo.Capture(ex));
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

            // il.Emit(OpCodes.Ldarg_0);
            // il.Emit(OpCodes.Ldfld, instanceField);
            // il.EmitCall(OpCodes.Callvirt, targetType.GetMethod("GetType"), null);
            il.Emit(OpCodes.Ret);
            propType.SetGetMethod(getPropType);

            return instanceField;
        }

        private static List<PropertyInfo> GetProperties(Type proxyType)
        {
            List<PropertyInfo> selectedProperties = new List<PropertyInfo>(proxyType.IsInterface ? proxyType.GetProperties() : GetBaseProperties(proxyType));
            Type[] implementedInterfaces = proxyType.GetInterfaces();
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

        private static void CreateProperties(TypeBuilder proxyTypeBuilder, Type proxyType, Type targetType, FieldInfo instanceField)
        {
            Version targetTypeAssemblyVersion = targetType.Assembly.GetName().Version;

            // Gets all properties to be implemented
            List<PropertyInfo> proxyTypeProperties = GetProperties(proxyType);

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
                                throw new DuckTypePropertyCantBeReadException(targetProperty);
                            }

                            propertyBuilder.SetGetMethod(GetPropertyGetMethod(proxyTypeBuilder, targetType, proxyProperty, targetProperty, instanceField));
                        }

                        if (proxyProperty.CanWrite)
                        {
                            // Check if the target property can be written
                            if (!targetProperty.CanWrite)
                            {
                                throw new DuckTypePropertyCantBeWrittenException(targetProperty);
                            }

                            // Check if the target property declaring type is an struct (structs modification is not supported)
                            if (targetProperty.DeclaringType.IsValueType)
                            {
                                throw new DuckTypeStructMembersCannotBeChangedException(targetProperty.DeclaringType);
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
                                throw new DuckTypeFieldIsReadonlyException(targetField);
                            }

                            // Check if the target field declaring type is an struct (structs modification is not supported)
                            if (targetField.DeclaringType.IsValueType)
                            {
                                throw new DuckTypeStructMembersCannotBeChangedException(targetField.DeclaringType);
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
                    throw new DuckTypePropertyOrFieldNotFoundException(proxyProperty.Name);
                }

                if (proxyProperty.CanWrite && propertyBuilder.SetMethod is null)
                {
                    throw new DuckTypePropertyOrFieldNotFoundException(proxyProperty.Name);
                }
            }
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

            private readonly Type _proxyType;
            private readonly ExceptionDispatchInfo _exceptionInfo;

            /// <summary>
            /// Initializes a new instance of the <see cref="CreateTypeResult"/> struct.
            /// </summary>
            /// <param name="proxyTypeDefinition">Proxy type definition class</param>
            /// <param name="exceptionInfo">Exception dispatch info instance</param>
            internal CreateTypeResult(Type proxyTypeDefinition, ExceptionDispatchInfo exceptionInfo)
            {
                _proxyType = proxyTypeDefinition;
                _exceptionInfo = exceptionInfo;
                Success = proxyTypeDefinition != null && exceptionInfo == null;
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
        }
    }
}
