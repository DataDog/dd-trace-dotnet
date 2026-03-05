// <copyright file="DuckType.Properties.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck Type
    /// </summary>
    public static partial class DuckType
    {
        private static MethodBuilder? GetPropertyGetMethod(
            TypeBuilder? proxyTypeBuilder,
            Type targetType,
            MemberInfo proxyMember,
            PropertyInfo targetProperty,
            FieldInfo? instanceField,
            Func<LazyILGenerator, Type, Type, Type> duckCastInnerToOuterFunc,
            Func<Type, Type, bool> needsDuckChaining)
        {
            MethodInfo? targetMethod = targetProperty.GetMethod;
            if (targetMethod is null)
            {
                return null;
            }

            string proxyMemberName = proxyMember.Name;
            Type proxyMemberReturnType = typeof(object);
            Type[] proxyParameterTypes = Type.EmptyTypes;
            Type[] targetParametersTypes = GetPropertyGetParametersTypes(proxyTypeBuilder, targetProperty, true).ToArray();

            if (proxyMember is PropertyInfo proxyProperty)
            {
                proxyMemberReturnType = proxyProperty.PropertyType;
                proxyParameterTypes = GetPropertyGetParametersTypes(proxyTypeBuilder, proxyProperty, true).ToArray();
                if (proxyParameterTypes.Length != targetParametersTypes.Length)
                {
                    DuckTypePropertyArgumentsLengthException.Throw(proxyProperty);
                }
            }
            else if (proxyMember is FieldInfo proxyField)
            {
                proxyMemberReturnType = proxyField.FieldType;
                proxyParameterTypes = Type.EmptyTypes;
                if (proxyParameterTypes.Length != targetParametersTypes.Length)
                {
                    DuckTypePropertyArgumentsLengthException.Throw(targetProperty);
                }
            }

            MethodBuilder? proxyMethod = proxyTypeBuilder?.DefineMethod(
                "get_" + proxyMemberName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                proxyMemberReturnType,
                proxyParameterTypes);

            var isValueWithType = false;
            var originalProxyMemberReturnType = proxyMemberReturnType;
            if (proxyMemberReturnType.IsGenericType && proxyMemberReturnType.GetGenericTypeDefinition() == typeof(ValueWithType<>))
            {
                proxyMemberReturnType = proxyMemberReturnType.GenericTypeArguments[0];
                isValueWithType = true;
            }

            LazyILGenerator il = new LazyILGenerator(proxyMethod?.GetILGenerator());
            Type returnType = targetProperty.PropertyType;

            // Load the instance if needed
            if (!targetMethod.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                if (instanceField is not null)
                {
                    il.Emit(instanceField.FieldType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld, instanceField);
                }
            }

            // Load the indexer keys to the stack
            for (int pIndex = 0; pIndex < proxyParameterTypes.Length; pIndex++)
            {
                Type proxyParamType = proxyParameterTypes[pIndex];
                Type targetParamType = targetParametersTypes[pIndex];

                // Check if the type can be converted of if we need to enable duck chaining
                if (NeedsDuckChaining(targetParamType, proxyParamType))
                {
                    // Load the argument and cast it as Duck type
                    il.WriteLoadArgument(pIndex, false);
                    il.Emit(OpCodes.Castclass, typeof(IDuckType));

                    // Call IDuckType.Instance property to get the actual value
                    il.EmitCall(OpCodes.Callvirt, DuckTypeInstancePropertyInfo.GetMethod!, null!);
                    targetParamType = typeof(object);
                }
                else
                {
                    il.WriteLoadArgument(pIndex, false);
                }

                // If the target parameter type is public or if it's by ref we have to actually use the original target type.
                targetParamType = UseDirectAccessTo(proxyTypeBuilder, targetParamType) || targetParamType.IsByRef ? targetParamType : typeof(object);
                il.WriteTypeConversion(proxyParamType, targetParamType);

                targetParametersTypes[pIndex] = targetParamType;
            }

            // Call the getter method
            if (UseDirectAccessTo(proxyTypeBuilder, targetType))
            {
                // If the instance is public we can emit directly without any dynamic method

                // Method call
                if (targetMethod.IsPublic)
                {
                    // We can emit a normal call if we have a public instance with a public property method.
                    il.EmitCall(targetMethod.IsStatic || (instanceField?.FieldType.IsValueType ?? false) ? OpCodes.Call : OpCodes.Callvirt, targetMethod, null!);
                }
                else
                {
                    // In case we have a public instance and a non public property method we can use [Calli] with the function pointer
                    il.WriteMethodCalli(targetMethod);
                }
            }
            else if (targetProperty.DeclaringType is not null && proxyTypeBuilder is not null && instanceField is not null)
            {
                // If the instance is not public we need to create a Dynamic method to overpass the visibility checks
                // we can't access non public types so we have to cast to object type (in the instance object and the return type).

                string dynMethodName = $"_getNonPublicProperty_{targetProperty.DeclaringType.Name}_{targetProperty.Name}";
                returnType = UseDirectAccessTo(proxyTypeBuilder, targetProperty.PropertyType) ? targetProperty.PropertyType : typeof(object);

                // We create the dynamic method
                Type[] targetParameters = GetPropertyGetParametersTypes(proxyTypeBuilder, targetProperty, false, !targetMethod.IsStatic).ToArray();
                Type[] dynParameters = targetMethod.IsStatic ? targetParametersTypes : (new[] { typeof(object) }).Concat(targetParametersTypes).ToArray();
                DynamicMethod dynMethod = new DynamicMethod(dynMethodName, returnType, dynParameters, proxyTypeBuilder.Module, true);

                // Emit the dynamic method body
                LazyILGenerator dynIL = new LazyILGenerator(dynMethod.GetILGenerator());

                if (!targetMethod.IsStatic)
                {
                    dynIL.LoadInstanceArgument(typeof(object), targetProperty.DeclaringType);
                }

                for (int idx = targetMethod.IsStatic ? 0 : 1; idx < dynParameters.Length; idx++)
                {
                    dynIL.WriteLoadArgument(idx, true);
                    dynIL.WriteTypeConversion(dynParameters[idx], targetParameters[idx]);
                }

                dynIL.EmitCall(targetMethod.IsStatic || instanceField.FieldType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, targetMethod, null!);
                dynIL.WriteTypeConversion(targetProperty.PropertyType, returnType);
                dynIL.Emit(OpCodes.Ret);
                dynIL.Flush();

                // Emit the call to the dynamic method
                il.WriteDynamicMethodCall(dynMethod, proxyTypeBuilder);
            }
            else
            {
                // Dry run: We enable all checks done in the preview if branch
                returnType = UseDirectAccessTo(proxyTypeBuilder, targetProperty.PropertyType) ? targetProperty.PropertyType : typeof(object);
                Type[] targetParameters = GetPropertyGetParametersTypes(proxyTypeBuilder, targetProperty, false, !targetMethod.IsStatic).ToArray();
                Type[] dynParameters = targetMethod.IsStatic ? targetParametersTypes : (new[] { typeof(object) }).Concat(targetParametersTypes).ToArray();
                for (int idx = targetMethod.IsStatic ? 0 : 1; idx < dynParameters.Length; idx++)
                {
                    ILHelpersExtensions.CheckTypeConversion(dynParameters[idx], targetParameters[idx]);
                }

                ILHelpersExtensions.CheckTypeConversion(targetProperty.PropertyType, returnType);
            }

            // Handle the return value
            // Check if the type can be converted or if we need to enable duck chaining
            if (needsDuckChaining(targetProperty.PropertyType, proxyMemberReturnType))
            {
                UseDirectAccessTo(proxyTypeBuilder, targetProperty.PropertyType);

                // If this is a forward duck type, we need to create a duck type from the original instance
                // If this is a reverse duck type, we need to cast to IDuckType and extract the original instance
                duckCastInnerToOuterFunc(il, proxyMemberReturnType, targetProperty.PropertyType);
            }
            else if (returnType != proxyMemberReturnType)
            {
                // If the type is not the expected type we try a conversion.
                il.WriteTypeConversion(returnType, proxyMemberReturnType);
            }

            if (isValueWithType)
            {
                il.Emit(OpCodes.Ldtoken, returnType);
                il.EmitCall(OpCodes.Call, GetTypeFromHandleMethodInfo, null!);
                il.EmitCall(OpCodes.Call, originalProxyMemberReturnType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public)!, null!);
            }

            il.Emit(OpCodes.Ret);
            il.Flush();
            if (proxyMethod is not null)
            {
                MethodBuilderGetToken.Invoke(proxyMethod, null);
            }

            return proxyMethod;
        }

        private static MethodBuilder? GetPropertySetMethod(
            TypeBuilder? proxyTypeBuilder,
            Type targetType,
            MemberInfo proxyMember,
            PropertyInfo targetProperty,
            FieldInfo? instanceField,
            Func<LazyILGenerator, Type, Type, Type> duckCastOuterToInner,
            Func<Type, Type, bool> needsDuckChaining)
        {
            MethodInfo? targetMethod = targetProperty.SetMethod;
            if (targetMethod is null)
            {
                return null;
            }

            string? proxyMemberName = null;
            Type[] proxyParameterTypes = Type.EmptyTypes;
            Type[] targetParametersTypes = GetPropertySetParametersTypes(proxyTypeBuilder, targetProperty, true).ToArray();

            if (proxyMember is PropertyInfo proxyProperty)
            {
                proxyMemberName = proxyProperty.Name;
                proxyParameterTypes = GetPropertySetParametersTypes(proxyTypeBuilder, proxyProperty, true).ToArray();
                if (proxyParameterTypes.Length != targetParametersTypes.Length)
                {
                    DuckTypePropertyArgumentsLengthException.Throw(proxyProperty);
                }
            }
            else if (proxyMember is FieldInfo proxyField)
            {
                proxyMemberName = proxyField.Name;
                proxyParameterTypes = new[] { proxyField.FieldType };
                if (proxyParameterTypes.Length != targetParametersTypes.Length)
                {
                    DuckTypePropertyArgumentsLengthException.Throw(targetProperty);
                }
            }

            MethodBuilder? proxyMethod = proxyTypeBuilder?.DefineMethod(
                "set_" + proxyMemberName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                typeof(void),
                proxyParameterTypes);

            LazyILGenerator il = new LazyILGenerator(proxyMethod?.GetILGenerator());

            // Load the instance if needed
            if (!targetMethod.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                if (instanceField is not null)
                {
                    il.Emit(instanceField.FieldType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld, instanceField);
                }
            }

            // Load the indexer keys and set value to the stack
            for (int pIndex = 0; pIndex < proxyParameterTypes.Length; pIndex++)
            {
                Type proxyParamType = proxyParameterTypes[pIndex];
                Type targetParamType = targetParametersTypes[pIndex];

                var isValueWithType = false;
                var originalProxyParamType = proxyParamType;
                if (proxyParamType.IsGenericType && proxyParamType.GetGenericTypeDefinition() == typeof(ValueWithType<>))
                {
                    proxyParamType = proxyParamType.GenericTypeArguments[0];
                    isValueWithType = true;
                }

                // Check if the type can be converted of if we need to enable duck chaining
                if (needsDuckChaining(targetParamType, proxyParamType))
                {
                    // Load the argument and cast it as Duck type
                    il.WriteLoadArgument(pIndex, false);
                    if (isValueWithType)
                    {
                        il.Emit(OpCodes.Ldfld, originalProxyParamType.GetField("Value")!);
                    }

                    // If this is a forward duck type, we need to cast to IDuckType and extract the original instance
                    // and set the targetParamType to object
                    // If this is a reverse duck type, we need to create a duck type from the original instance
                    targetParamType = duckCastOuterToInner(il, targetParamType, proxyParamType);
                }
                else
                {
                    il.WriteLoadArgument(pIndex, false);
                    if (isValueWithType)
                    {
                        il.Emit(OpCodes.Ldfld, originalProxyParamType.GetField("Value")!);
                    }
                }

                // If the target parameter type is public or if it's by ref we have to actually use the original target type.
                targetParamType = UseDirectAccessTo(proxyTypeBuilder, targetParamType) || targetParamType.IsByRef ? targetParamType : typeof(object);
                il.WriteTypeConversion(proxyParamType, targetParamType);

                targetParametersTypes[pIndex] = targetParamType;
            }

            // Call the setter method
            if (UseDirectAccessTo(proxyTypeBuilder, targetType))
            {
                // If the instance is public we can emit directly without any dynamic method

                if (targetMethod.IsPublic)
                {
                    // We can emit a normal call if we have a public instance with a public property method.
                    il.EmitCall(targetMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, targetMethod, null!);
                }
                else
                {
                    // In case we have a public instance and a non public property method we can use [Calli] with the function pointer
                    il.WriteMethodCalli(targetMethod);
                }
            }
            else if (targetProperty.DeclaringType is not null && proxyTypeBuilder is not null && instanceField is not null)
            {
                // If the instance is not public we need to create a Dynamic method to overpass the visibility checks
                // we can't access non public types so we have to cast to object type (in the instance object and the return type).

                string dynMethodName = $"_setNonPublicProperty+{targetProperty.DeclaringType.Name}.{targetProperty.Name}";

                // We create the dynamic method
                Type[] targetParameters = GetPropertySetParametersTypes(proxyTypeBuilder, targetProperty, false, !targetMethod.IsStatic).ToArray();
                Type[] dynParameters = targetMethod.IsStatic ? targetParametersTypes : (new[] { typeof(object) }).Concat(targetParametersTypes).ToArray();
                DynamicMethod dynMethod = new DynamicMethod(dynMethodName, typeof(void), dynParameters, proxyTypeBuilder.Module, true);

                // Emit the dynamic method body
                LazyILGenerator dynIL = new LazyILGenerator(dynMethod.GetILGenerator());

                if (!targetMethod.IsStatic)
                {
                    dynIL.LoadInstanceArgument(typeof(object), targetProperty.DeclaringType);
                }

                for (int idx = targetMethod.IsStatic ? 0 : 1; idx < dynParameters.Length; idx++)
                {
                    dynIL.WriteLoadArgument(idx, true);
                    dynIL.WriteTypeConversion(dynParameters[idx], targetParameters[idx]);
                }

                dynIL.EmitCall(targetMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, targetMethod, null!);
                dynIL.Emit(OpCodes.Ret);
                dynIL.Flush();

                // Create and load delegate for the DynamicMethod
                il.WriteDynamicMethodCall(dynMethod, proxyTypeBuilder);
            }
            else
            {
                Type[] targetParameters = GetPropertySetParametersTypes(proxyTypeBuilder, targetProperty, false, !targetMethod.IsStatic).ToArray();
                Type[] dynParameters = targetMethod.IsStatic ? targetParametersTypes : (new[] { typeof(object) }).Concat(targetParametersTypes).ToArray();
                for (int idx = targetMethod.IsStatic ? 0 : 1; idx < dynParameters.Length; idx++)
                {
                    ILHelpersExtensions.CheckTypeConversion(dynParameters[idx], targetParameters[idx]);
                }
            }

            il.Emit(OpCodes.Ret);
            il.Flush();
            if (proxyMethod is not null)
            {
                MethodBuilderGetToken.Invoke(proxyMethod, null);
            }

            return proxyMethod;
        }

        private static IEnumerable<Type> GetPropertyGetParametersTypes(TypeBuilder? typeBuilder, PropertyInfo property, bool originalTypes, bool isDynamicSignature = false)
        {
            if (isDynamicSignature)
            {
                yield return typeof(object);
            }

            ParameterInfo[] idxParams = property.GetIndexParameters();
            foreach (ParameterInfo parameter in idxParams)
            {
                if (originalTypes || UseDirectAccessTo(typeBuilder, parameter.ParameterType))
                {
                    yield return parameter.ParameterType;
                }
                else
                {
                    yield return typeof(object);
                }
            }
        }

        private static IEnumerable<Type> GetPropertySetParametersTypes(TypeBuilder? typeBuilder, PropertyInfo property, bool originalTypes, bool isDynamicSignature = false)
        {
            if (isDynamicSignature)
            {
                yield return typeof(object);
            }

            foreach (Type indexType in GetPropertyGetParametersTypes(typeBuilder, property, originalTypes))
            {
                yield return indexType;
            }

            if (originalTypes || UseDirectAccessTo(typeBuilder, property.PropertyType))
            {
                yield return property.PropertyType;
            }
            else
            {
                yield return typeof(object);
            }
        }
    }
}
