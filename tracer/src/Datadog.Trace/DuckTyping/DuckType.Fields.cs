// <copyright file="DuckType.Fields.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck Type
    /// </summary>
    public static partial class DuckType
    {
        private static MethodBuilder? GetFieldGetMethod(
            TypeBuilder? proxyTypeBuilder,
            Type targetType,
            MemberInfo proxyMember,
            FieldInfo targetField,
            FieldInfo? instanceField)
        {
            string proxyMemberName = proxyMember.Name;
            Type proxyMemberReturnType = proxyMember is PropertyInfo pinfo ? pinfo.PropertyType : proxyMember is FieldInfo finfo ? finfo.FieldType : typeof(object);

            MethodBuilder? proxyMethod = proxyTypeBuilder?.DefineMethod(
                "get_" + proxyMemberName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                proxyMemberReturnType,
                Type.EmptyTypes);

            var isValueWithType = false;
            var originalProxyMemberReturnType = proxyMemberReturnType;
            if (proxyMemberReturnType.IsGenericType && proxyMemberReturnType.GetGenericTypeDefinition() == typeof(ValueWithType<>))
            {
                proxyMemberReturnType = proxyMemberReturnType.GenericTypeArguments[0];
                isValueWithType = true;
            }

            LazyILGenerator il = new LazyILGenerator(proxyMethod?.GetILGenerator());
            Type returnType = targetField.FieldType;

            // Load the field value to the stack
            if (UseDirectAccessTo(proxyTypeBuilder, targetType))
            {
                // Load the instance
                if (!targetField.IsStatic)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    if (instanceField is not null)
                    {
                        il.Emit(instanceField.FieldType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld, instanceField);
                    }
                }

                // In case is public is pretty simple
                il.Emit(targetField.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, targetField);
            }
            else if (targetField.DeclaringType is not null && proxyTypeBuilder is not null)
            {
                // Load the instance
                if (!targetField.IsStatic)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    if (instanceField is not null)
                    {
                        il.Emit(OpCodes.Ldfld, instanceField);
                        if (instanceField.FieldType.IsValueType)
                        {
                            il.Emit(OpCodes.Box, instanceField.FieldType);
                        }
                    }
                }

                // If the instance or the field are non public we need to create a Dynamic method to overpass the visibility checks
                // we can't access non public types so we have to cast to object type (in the instance object and the return type if is needed).
                string dynMethodName = $"_getNonPublicField_{targetField.DeclaringType.Name}_{targetField.Name}";
                returnType = UseDirectAccessTo(proxyTypeBuilder, targetField.FieldType) ? targetField.FieldType : typeof(object);

                // We create the dynamic method
                Type[] dynParameters = targetField.IsStatic ? Type.EmptyTypes : new[] { typeof(object) };
                DynamicMethod dynMethod = new DynamicMethod(dynMethodName, returnType, dynParameters, proxyTypeBuilder.Module, true);

                // Emit the dynamic method body
                LazyILGenerator dynIL = new LazyILGenerator(dynMethod.GetILGenerator());

                if (!targetField.IsStatic)
                {
                    // Emit the instance load in the dynamic method
                    dynIL.Emit(OpCodes.Ldarg_0);
                    if (targetField.DeclaringType != typeof(object))
                    {
                        dynIL.Emit(targetField.DeclaringType!.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, targetField.DeclaringType);
                    }
                }

                // Emit the field and convert before returning (in case of boxing)
                dynIL.Emit(targetField.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, targetField);
                dynIL.WriteTypeConversion(targetField.FieldType, returnType);
                dynIL.Emit(OpCodes.Ret);
                dynIL.Flush();

                // Emit the call to the dynamic method
                il.WriteDynamicMethodCall(dynMethod, proxyTypeBuilder);
            }
            else
            {
                // Dry run: We enable all checks done in the preview if branch
                returnType = UseDirectAccessTo(proxyTypeBuilder, targetField.FieldType) ? targetField.FieldType : typeof(object);
                ILHelpersExtensions.CheckTypeConversion(targetField.FieldType, returnType);
            }

            // Check if the type can be converted or if we need to enable duck chaining
            if (NeedsDuckChaining(targetField.FieldType, proxyMemberReturnType))
            {
                UseDirectAccessTo(proxyTypeBuilder, targetField.FieldType);

                // WARNING: If targetField.FieldType cannot be duck cast to proxyMemberReturnType
                // this will throw an exception at runtime when accessing the member
                // We call DuckType.CreateCache<>.Create()
                MethodIlHelper.AddIlToDuckChain(il, proxyMemberReturnType, targetField.FieldType);
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

        private static MethodBuilder? GetFieldSetMethod(
            TypeBuilder? proxyTypeBuilder,
            Type targetType,
            MemberInfo proxyMember,
            FieldInfo targetField,
            FieldInfo? instanceField)
        {
            string proxyMemberName = proxyMember.Name;
            Type proxyMemberReturnType = proxyMember is PropertyInfo pinfo ? pinfo.PropertyType : proxyMember is FieldInfo finfo ? finfo.FieldType : typeof(object);

            MethodBuilder? method = proxyTypeBuilder?.DefineMethod(
                "set_" + proxyMemberName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                typeof(void),
                new[] { proxyMemberReturnType });

            LazyILGenerator il = new LazyILGenerator(method?.GetILGenerator());
            Type currentValueType = proxyMemberReturnType;

            // Load instance
            if (!targetField.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                if (instanceField is not null)
                {
                    il.Emit(instanceField.FieldType.IsValueType ? OpCodes.Ldflda : OpCodes.Ldfld, instanceField);
                }
            }

            var isValueWithType = false;
            var originalproxyMemberReturnType = proxyMemberReturnType;
            if (proxyMemberReturnType.IsGenericType && proxyMemberReturnType.GetGenericTypeDefinition() == typeof(ValueWithType<>))
            {
                proxyMemberReturnType = proxyMemberReturnType.GenericTypeArguments[0];
                currentValueType = proxyMemberReturnType;
                isValueWithType = true;
            }

            // Check if the type can be converted of if we need to enable duck chaining
            if (NeedsDuckChaining(targetField.FieldType, proxyMemberReturnType))
            {
                // Load the argument and convert it to Duck type
                il.Emit(OpCodes.Ldarg_1);
                if (isValueWithType)
                {
                    il.Emit(OpCodes.Ldfld, originalproxyMemberReturnType.GetField("Value")!);
                }

                il.WriteTypeConversion(proxyMemberReturnType, typeof(IDuckType));

                // Call IDuckType.Instance property to get the actual value
                il.EmitCall(OpCodes.Callvirt, DuckTypeInstancePropertyInfo.GetMethod!, null!);

                currentValueType = typeof(object);
            }
            else
            {
                // Load the value into the stack
                il.Emit(OpCodes.Ldarg_1);
                if (isValueWithType)
                {
                    il.Emit(OpCodes.Ldfld, originalproxyMemberReturnType.GetField("Value")!);
                }
            }

            // We set the field value
            if (UseDirectAccessTo(proxyTypeBuilder, targetType))
            {
                // If the instance and the field are public then is easy to set.
                il.WriteTypeConversion(currentValueType, targetField.FieldType);

                il.Emit(targetField.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, targetField);
            }
            else if (targetField.DeclaringType is not null && proxyTypeBuilder is not null)
            {
                // If the instance or the field are non public we need to create a Dynamic method to overpass the visibility checks
                string dynMethodName = $"_setField_{targetField.DeclaringType.Name}_{targetField.Name}";

                // Convert the field type for the dynamic method
                Type dynValueType = UseDirectAccessTo(proxyTypeBuilder, targetField.FieldType) ? targetField.FieldType : typeof(object);
                il.WriteTypeConversion(currentValueType, dynValueType);

                // Create dynamic method
                Type[] dynParameters = targetField.IsStatic ? new[] { dynValueType } : new[] { typeof(object), dynValueType };
                DynamicMethod dynMethod = new DynamicMethod(dynMethodName, typeof(void), dynParameters, proxyTypeBuilder.Module, true);

                // Write the dynamic method body
                LazyILGenerator dynIL = new LazyILGenerator(dynMethod.GetILGenerator());
                dynIL.Emit(OpCodes.Ldarg_0);

                if (targetField.IsStatic)
                {
                    dynIL.WriteTypeConversion(dynValueType, targetField.FieldType);
                    dynIL.Emit(OpCodes.Stsfld, targetField);
                }
                else
                {
                    if (targetField.DeclaringType != typeof(object))
                    {
                        dynIL.Emit(OpCodes.Castclass, targetField.DeclaringType);
                    }

                    dynIL.Emit(OpCodes.Ldarg_1);
                    dynIL.WriteTypeConversion(dynValueType, targetField.FieldType);
                    dynIL.Emit(OpCodes.Stfld, targetField);
                }

                dynIL.Emit(OpCodes.Ret);
                dynIL.Flush();

                // Emit the call to the dynamic method
                il.WriteDynamicMethodCall(dynMethod, proxyTypeBuilder);
            }
            else
            {
                // Dry run: We enable all checks done in the preview if branch
                Type dynValueType = UseDirectAccessTo(proxyTypeBuilder, targetField.FieldType) ? targetField.FieldType : typeof(object);
                ILHelpersExtensions.CheckTypeConversion(currentValueType, dynValueType);
                ILHelpersExtensions.CheckTypeConversion(dynValueType, targetField.FieldType);
            }

            il.Emit(OpCodes.Ret);
            il.Flush();
            if (method is not null)
            {
                MethodBuilderGetToken.Invoke(method, null);
            }

            return method;
        }
    }
}
