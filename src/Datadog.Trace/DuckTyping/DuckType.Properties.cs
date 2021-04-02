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
        private static MethodBuilder GetPropertyGetMethod(TypeBuilder proxyTypeBuilder, Type targetType, MemberInfo proxyMember, PropertyInfo targetProperty, FieldInfo instanceField)
        {
            string proxyMemberName = proxyMember.Name;
            Type proxyMemberReturnType = typeof(object);
            Type[] proxyParameterTypes = Type.EmptyTypes;
            Type[] targetParametersTypes = GetPropertyGetParametersTypes(targetProperty, true).ToArray();

            if (proxyMember is PropertyInfo proxyProperty)
            {
                proxyMemberReturnType = proxyProperty.PropertyType;
                proxyParameterTypes = GetPropertyGetParametersTypes(proxyProperty, true).ToArray();
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

            MethodBuilder proxyMethod = proxyTypeBuilder.DefineMethod(
                "get_" + proxyMemberName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                proxyMemberReturnType,
                proxyParameterTypes);

            LazyILGenerator il = new LazyILGenerator(proxyMethod.GetILGenerator());
            MethodInfo targetMethod = targetProperty.GetMethod;
            Type returnType = targetProperty.PropertyType;

            // Load the instance if needed
            if (!targetMethod.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(instanceField.FieldType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld, instanceField);
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
                    il.EmitCall(OpCodes.Callvirt, DuckTypeInstancePropertyInfo.GetMethod, null);
                    targetParamType = typeof(object);
                }
                else
                {
                    il.WriteLoadArgument(pIndex, false);
                }

                // If the target parameter type is public or if it's by ref we have to actually use the original target type.
                targetParamType = UseDirectAccessTo(targetParamType) || targetParamType.IsByRef ? targetParamType : typeof(object);
                il.WriteTypeConversion(proxyParamType, targetParamType);

                targetParametersTypes[pIndex] = targetParamType;
            }

            // Call the getter method
            if (UseDirectAccessTo(targetType))
            {
                // If the instance is public we can emit directly without any dynamic method

                // Method call
                if (targetMethod.IsPublic)
                {
                    // We can emit a normal call if we have a public instance with a public property method.
                    il.EmitCall(targetMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, targetMethod, null);
                }
                else
                {
                    // In case we have a public instance and a non public property method we can use [Calli] with the function pointer
                    il.WriteMethodCalli(targetMethod);
                }
            }
            else
            {
                // If the instance is not public we need to create a Dynamic method to overpass the visibility checks
                // we can't access non public types so we have to cast to object type (in the instance object and the return type).

                string dynMethodName = $"_getNonPublicProperty_{targetProperty.DeclaringType.Name}_{targetProperty.Name}";
                returnType = UseDirectAccessTo(targetProperty.PropertyType) ? targetProperty.PropertyType : typeof(object);

                // We create the dynamic method
                Type[] targetParameters = GetPropertyGetParametersTypes(targetProperty, false, !targetMethod.IsStatic).ToArray();
                Type[] dynParameters = targetMethod.IsStatic ? targetParametersTypes : (new[] { typeof(object) }).Concat(targetParametersTypes).ToArray();
                DynamicMethod dynMethod = new DynamicMethod(dynMethodName, returnType, dynParameters, _moduleBuilder, true);

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

                dynIL.EmitCall(targetMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, targetMethod, null);
                dynIL.WriteTypeConversion(targetProperty.PropertyType, returnType);
                dynIL.Emit(OpCodes.Ret);
                dynIL.Flush();

                // Emit the call to the dynamic method
                il.WriteDynamicMethodCall(dynMethod, proxyTypeBuilder);
            }

            // Handle the return value
            // Check if the type can be converted or if we need to enable duck chaining
            if (NeedsDuckChaining(targetProperty.PropertyType, proxyMemberReturnType))
            {
                if (UseDirectAccessTo(targetProperty.PropertyType) && targetProperty.PropertyType.IsValueType)
                {
                    il.Emit(OpCodes.Box, targetProperty.PropertyType);
                }

                // We call DuckType.CreateCache<>.Create()
                MethodInfo getProxyMethodInfo = typeof(CreateCache<>)
                    .MakeGenericType(proxyMemberReturnType).GetMethod("Create");

                il.Emit(OpCodes.Call, getProxyMethodInfo);
            }
            else if (returnType != proxyMemberReturnType)
            {
                // If the type is not the expected type we try a conversion.
                il.WriteTypeConversion(returnType, proxyMemberReturnType);
            }

            il.Emit(OpCodes.Ret);
            il.Flush();
            _methodBuilderGetToken.Invoke(proxyMethod, null);
            return proxyMethod;
        }

        private static MethodBuilder GetPropertySetMethod(TypeBuilder proxyTypeBuilder, Type targetType, MemberInfo proxyMember, PropertyInfo targetProperty, FieldInfo instanceField)
        {
            string proxyMemberName = null;
            Type[] proxyParameterTypes = Type.EmptyTypes;
            Type[] targetParametersTypes = GetPropertySetParametersTypes(targetProperty, true).ToArray();

            if (proxyMember is PropertyInfo proxyProperty)
            {
                proxyMemberName = proxyProperty.Name;
                proxyParameterTypes = GetPropertySetParametersTypes(proxyProperty, true).ToArray();
                if (proxyParameterTypes.Length != targetParametersTypes.Length)
                {
                    DuckTypePropertyArgumentsLengthException.Throw(proxyProperty);
                }
            }
            else if (proxyMember is FieldInfo proxyField)
            {
                proxyMemberName = proxyField.Name;
                proxyParameterTypes = new Type[] { proxyField.FieldType };
                if (proxyParameterTypes.Length != targetParametersTypes.Length)
                {
                    DuckTypePropertyArgumentsLengthException.Throw(targetProperty);
                }
            }

            MethodBuilder proxyMethod = proxyTypeBuilder.DefineMethod(
                "set_" + proxyMemberName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                typeof(void),
                proxyParameterTypes);

            LazyILGenerator il = new LazyILGenerator(proxyMethod.GetILGenerator());
            MethodInfo targetMethod = targetProperty.SetMethod;

            // Load the instance if needed
            if (!targetMethod.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(instanceField.FieldType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld, instanceField);
            }

            // Load the indexer keys and set value to the stack
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
                    il.EmitCall(OpCodes.Callvirt, DuckTypeInstancePropertyInfo.GetMethod, null);

                    targetParamType = typeof(object);
                }
                else
                {
                    il.WriteLoadArgument(pIndex, false);
                }

                // If the target parameter type is public or if it's by ref we have to actually use the original target type.
                targetParamType = UseDirectAccessTo(targetParamType) || targetParamType.IsByRef ? targetParamType : typeof(object);
                il.WriteTypeConversion(proxyParamType, targetParamType);

                targetParametersTypes[pIndex] = targetParamType;
            }

            // Call the setter method
            if (UseDirectAccessTo(targetType))
            {
                // If the instance is public we can emit directly without any dynamic method

                if (targetMethod.IsPublic)
                {
                    // We can emit a normal call if we have a public instance with a public property method.
                    il.EmitCall(targetMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, targetMethod, null);
                }
                else
                {
                    // In case we have a public instance and a non public property method we can use [Calli] with the function pointer
                    il.WriteMethodCalli(targetMethod);
                }
            }
            else
            {
                // If the instance is not public we need to create a Dynamic method to overpass the visibility checks
                // we can't access non public types so we have to cast to object type (in the instance object and the return type).

                string dynMethodName = $"_setNonPublicProperty+{targetProperty.DeclaringType.Name}.{targetProperty.Name}";

                // We create the dynamic method
                Type[] targetParameters = GetPropertySetParametersTypes(targetProperty, false, !targetMethod.IsStatic).ToArray();
                Type[] dynParameters = targetMethod.IsStatic ? targetParametersTypes : (new[] { typeof(object) }).Concat(targetParametersTypes).ToArray();
                DynamicMethod dynMethod = new DynamicMethod(dynMethodName, typeof(void), dynParameters, _moduleBuilder, true);

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

                dynIL.EmitCall(targetMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, targetMethod, null);
                dynIL.Emit(OpCodes.Ret);
                dynIL.Flush();

                // Create and load delegate for the DynamicMethod
                il.WriteDynamicMethodCall(dynMethod, proxyTypeBuilder);
            }

            il.Emit(OpCodes.Ret);
            il.Flush();
            _methodBuilderGetToken.Invoke(proxyMethod, null);
            return proxyMethod;
        }

        private static IEnumerable<Type> GetPropertyGetParametersTypes(PropertyInfo property, bool originalTypes, bool isDynamicSignature = false)
        {
            if (isDynamicSignature)
            {
                yield return typeof(object);
            }

            ParameterInfo[] idxParams = property.GetIndexParameters();
            foreach (ParameterInfo parameter in idxParams)
            {
                if (originalTypes || UseDirectAccessTo(parameter.ParameterType))
                {
                    yield return parameter.ParameterType;
                }
                else
                {
                    yield return typeof(object);
                }
            }
        }

        private static IEnumerable<Type> GetPropertySetParametersTypes(PropertyInfo property, bool originalTypes, bool isDynamicSignature = false)
        {
            if (isDynamicSignature)
            {
                yield return typeof(object);
            }

            foreach (Type indexType in GetPropertyGetParametersTypes(property, originalTypes))
            {
                yield return indexType;
            }

            if (originalTypes || UseDirectAccessTo(property.PropertyType))
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
