using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Duck Type
    /// </summary>
    public static partial class DuckType
    {
        private static MethodBuilder GetFieldGetMethod(TypeBuilder proxyTypeBuilder, Type targetType, PropertyInfo proxyProperty, FieldInfo targetField, FieldInfo instanceField)
        {
            MethodBuilder proxyMethod = proxyTypeBuilder.DefineMethod(
                "get_" + proxyProperty.Name,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                proxyProperty.PropertyType,
                Type.EmptyTypes);

            ILGenerator il = proxyMethod.GetILGenerator();
            bool isPublicInstance = targetType.IsPublic || targetType.IsNestedPublic;
            Type returnType = targetField.FieldType;

            // Load the instance
            if (!targetField.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, instanceField);
            }

            // Load the field value to the stack
            if (isPublicInstance && targetField.IsPublic)
            {
                // In case is public is pretty simple
                il.Emit(targetField.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, targetField);
            }
            else
            {
                // If the instance or the field are non public we need to create a Dynamic method to overpass the visibility checks
                // we can't access non public types so we have to cast to object type (in the instance object and the return type).

                string dynMethodName = $"_getNonPublicField+{targetField.DeclaringType.Name}.{targetField.Name}";
                returnType = targetField.FieldType.IsPublic || targetField.FieldType.IsNestedPublic ? targetField.FieldType : typeof(object);

                // We create the dynamic method
                Type[] dynParameters = targetField.IsStatic ? Type.EmptyTypes : new[] { typeof(object) };
                DynamicMethod dynMethod = new DynamicMethod(dynMethodName, returnType, dynParameters, typeof(DuckType).Module, true);

                // We store the dynamic method in a bag to avoid getting collected by the GC.
                DynamicMethods.Add(dynMethod);

                // Emit the dynamic method body
                ILGenerator dynIL = dynMethod.GetILGenerator();

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
                ILHelpers.TypeConversion(dynIL, targetField.FieldType, returnType);
                dynIL.Emit(OpCodes.Ret);

                // Emit the Call to the dynamic method pointer [Calli]
                il.Emit(OpCodes.Ldc_I8, (long)GetRuntimeHandle(dynMethod).GetFunctionPointer());
                il.Emit(OpCodes.Conv_I);
                il.EmitCalli(OpCodes.Calli, dynMethod.CallingConvention, returnType, dynParameters, null);
            }

            // Check if the type can be converted or if we need to enable duck chaining
            if (proxyProperty.PropertyType != targetField.FieldType && !proxyProperty.PropertyType.IsValueType && !proxyProperty.PropertyType.IsAssignableFrom(targetField.FieldType))
            {
                // If we are in a duck chaining scenario we convert the field value to an object and push it to the stack
                ILHelpers.TypeConversion(il, returnType, typeof(object));

                // Load the property type to the stack
                il.Emit(OpCodes.Ldtoken, proxyProperty.PropertyType);
                il.EmitCall(OpCodes.Call, GetTypeFromHandleMethodInfo, null);

                // We call DuckType.GetStructDuckTypeChainningValue() with the 2 loaded values from the stack: field value, property type
                il.EmitCall(OpCodes.Call, GetDuckTypeChainningValueMethodInfo, null);

                // Convert the ducktype child to the actual property type
                ILHelpers.TypeConversion(il, typeof(IDuckType), proxyProperty.PropertyType);
            }
            else if (returnType != proxyProperty.PropertyType)
            {
                // If the type is not the expected type we try a conversion.
                ILHelpers.TypeConversion(il, returnType, proxyProperty.PropertyType);
            }

            il.Emit(OpCodes.Ret);
            return proxyMethod;
        }

        private static MethodBuilder GetFieldSetMethod(TypeBuilder proxyTypeBuilder, Type targetType, PropertyInfo proxyProperty, FieldInfo targetField, FieldInfo instanceField)
        {
            MethodBuilder method = proxyTypeBuilder.DefineMethod(
                "set_" + proxyProperty.Name,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                typeof(void),
                new[] { proxyProperty.PropertyType });

            ILGenerator il = method.GetILGenerator();
            bool isPublicInstance = targetType.IsPublic || targetType.IsNestedPublic;
            Type currentValueType = proxyProperty.PropertyType;

            // Load instance
            if (!targetField.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, instanceField);
            }

            // Check if the type can be converted of if we need to enable duck chaining
            if (proxyProperty.PropertyType != targetField.FieldType && !proxyProperty.PropertyType.IsValueType && !proxyProperty.PropertyType.IsAssignableFrom(targetField.FieldType))
            {
                // Load the argument and convert it to Duck type
                il.Emit(OpCodes.Ldarg_1);
                ILHelpers.TypeConversion(il, proxyProperty.PropertyType, typeof(IDuckType));

                // Call IDuckType.Instance property to get the actual value
                il.EmitCall(OpCodes.Callvirt, DuckTypeInstancePropertyInfo.GetMethod, null);

                currentValueType = typeof(object);
            }
            else
            {
                // Load the value into the stack
                il.Emit(OpCodes.Ldarg_1);
            }

            // We set the field value
            if (isPublicInstance && targetField.IsPublic)
            {
                // If the instance and the field are public then is easy to set.
                ILHelpers.TypeConversion(il, currentValueType, targetField.FieldType);

                il.Emit(targetField.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, targetField);
            }
            else
            {
                // If the instance or the field are non public we need to create a Dynamic method to overpass the visibility checks

                string dynMethodName = $"_setField+{targetField.DeclaringType.Name}.{targetField.Name}";

                // Convert the field type for the dynamic method
                Type dynValueType = targetField.FieldType.IsPublic || targetField.FieldType.IsNestedPublic ? targetField.FieldType : typeof(object);
                ILHelpers.TypeConversion(il, currentValueType, dynValueType);

                // Create dynamic method
                Type[] dynParameters = targetField.IsStatic ? new[] { dynValueType } : new[] { typeof(object), dynValueType };
                DynamicMethod dynMethod = new DynamicMethod(dynMethodName, typeof(void), dynParameters, typeof(DuckType).Module, true);
                DynamicMethods.Add(dynMethod);

                // Write the dynamic method body
                ILGenerator dynIL = dynMethod.GetILGenerator();
                dynIL.Emit(OpCodes.Ldarg_0);

                if (targetField.IsStatic)
                {
                    ILHelpers.TypeConversion(dynIL, dynValueType, targetField.FieldType);
                    dynIL.Emit(OpCodes.Stsfld, targetField);
                }
                else
                {
                    if (targetField.DeclaringType != typeof(object))
                    {
                        dynIL.Emit(OpCodes.Castclass, targetField.DeclaringType);
                    }

                    dynIL.Emit(OpCodes.Ldarg_1);
                    ILHelpers.TypeConversion(dynIL, dynValueType, targetField.FieldType);
                    dynIL.Emit(OpCodes.Stfld, targetField);
                }

                dynIL.Emit(OpCodes.Ret);

                // Emit the call to the dynamic method
                il.Emit(OpCodes.Ldc_I8, (long)GetRuntimeHandle(dynMethod).GetFunctionPointer());
                il.Emit(OpCodes.Conv_I);
                il.EmitCalli(OpCodes.Calli, dynMethod.CallingConvention, typeof(void), dynParameters, null);
            }

            il.Emit(OpCodes.Ret);
            return method;
        }
    }
}
