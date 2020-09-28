using System;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Internal IL Helpers
    /// </summary>
    internal static class ILHelpers
    {
        /// <summary>
        /// Load instance argument
        /// </summary>
        /// <param name="il">ILGenerator</param>
        /// <param name="actualType">Actual type</param>
        /// <param name="expectedType">Expected type</param>
        internal static void LoadInstanceArgument(ILGenerator il, Type actualType, Type expectedType)
        {
            il.Emit(OpCodes.Ldarg_0);
            if (actualType == expectedType)
            {
                return;
            }

            if (expectedType.IsValueType)
            {
                il.DeclareLocal(expectedType);
                il.Emit(OpCodes.Unbox_Any, expectedType);
                il.Emit(OpCodes.Stloc_0);
                il.Emit(OpCodes.Ldloca_S, 0);
            }
            else
            {
                il.Emit(OpCodes.Castclass, expectedType);
            }
        }

        /// <summary>
        /// Write load arguments
        /// </summary>
        /// <param name="index">Argument index</param>
        /// <param name="il">IlGenerator</param>
        /// <param name="isStatic">Define if we need to take into account the instance argument</param>
        internal static void WriteLoadArgument(int index, ILGenerator il, bool isStatic)
        {
            switch (index)
            {
                case 0:
                    il.Emit(isStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
                    break;
                case 1:
                    il.Emit(isStatic ? OpCodes.Ldarg_1 : OpCodes.Ldarg_2);
                    break;
                case 2:
                    il.Emit(isStatic ? OpCodes.Ldarg_2 : OpCodes.Ldarg_3);
                    break;
                case 3:
                    if (isStatic)
                    {
                        il.Emit(OpCodes.Ldarg_3);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldarg_S, 4);
                    }

                    break;
                default:
                    il.Emit(OpCodes.Ldarg_S, isStatic ? index : index + 1);
                    break;
            }
        }

        /// <summary>
        /// Write load local
        /// </summary>
        /// <param name="index">Local index</param>
        /// <param name="il">IlGenerator</param>
        internal static void WriteLoadLocal(int index, ILGenerator il)
        {
            switch (index)
            {
                case 0:
                    il.Emit(OpCodes.Ldloc_0);
                    break;
                case 1:
                    il.Emit(OpCodes.Ldloc_1);
                    break;
                case 2:
                    il.Emit(OpCodes.Ldloc_2);
                    break;
                case 3:
                    il.Emit(OpCodes.Ldloc_3);
                    break;
                default:
                    il.Emit(OpCodes.Ldloc_S, index);
                    break;
            }
        }

        /// <summary>
        /// Write store local
        /// </summary>
        /// <param name="index">Local index</param>
        /// <param name="il">IlGenerator</param>
        internal static void WriteStoreLocal(int index, ILGenerator il)
        {
            switch (index)
            {
                case 0:
                    il.Emit(OpCodes.Stloc_0);
                    break;
                case 1:
                    il.Emit(OpCodes.Stloc_1);
                    break;
                case 2:
                    il.Emit(OpCodes.Stloc_2);
                    break;
                case 3:
                    il.Emit(OpCodes.Stloc_3);
                    break;
                default:
                    il.Emit(OpCodes.Stloc_S, index);
                    break;
            }
        }

        /// <summary>
        /// Write int value
        /// </summary>
        /// <param name="il">ILGenerator</param>
        /// <param name="value">Integer value</param>
        internal static void WriteIlIntValue(ILGenerator il, int value)
        {
            switch (value)
            {
                case 0:
                    il.Emit(OpCodes.Ldc_I4_0);
                    break;
                case 1:
                    il.Emit(OpCodes.Ldc_I4_1);
                    break;
                case 2:
                    il.Emit(OpCodes.Ldc_I4_2);
                    break;
                case 3:
                    il.Emit(OpCodes.Ldc_I4_3);
                    break;
                case 4:
                    il.Emit(OpCodes.Ldc_I4_4);
                    break;
                case 5:
                    il.Emit(OpCodes.Ldc_I4_5);
                    break;
                case 6:
                    il.Emit(OpCodes.Ldc_I4_6);
                    break;
                case 7:
                    il.Emit(OpCodes.Ldc_I4_7);
                    break;
                case 8:
                    il.Emit(OpCodes.Ldc_I4_8);
                    break;
                default:
                    il.Emit(OpCodes.Ldc_I4_S, value);
                    break;
            }
        }

        /// <summary>
        /// Convert a current type to an expected type
        /// </summary>
        /// <param name="il">ILGenerator</param>
        /// <param name="actualType">Actual type</param>
        /// <param name="expectedType">Expected type</param>
        internal static void TypeConversion(ILGenerator il, Type actualType, Type expectedType)
        {
            if (actualType == expectedType)
            {
                return;
            }

            if (actualType.IsGenericParameter && expectedType.IsGenericParameter)
            {
                return;
            }

            var actualUnderlyingType = actualType.IsEnum ? Enum.GetUnderlyingType(actualType) : actualType;
            var expectedUnderlyingType = expectedType.IsEnum ? Enum.GetUnderlyingType(expectedType) : expectedType;

            if (actualUnderlyingType.IsValueType)
            {
                if (expectedUnderlyingType.IsValueType && actualUnderlyingType != expectedUnderlyingType)
                {
                    // If both underlying types are value types then both must be of the same type.
                    throw new InvalidCastException();
                }
                else if (!expectedUnderlyingType.IsValueType)
                {
                    // An underlying type can be boxed and converted to an object or interface type if the actual type support this
                    // if not we should throw.
                    if (expectedUnderlyingType == typeof(object) || expectedUnderlyingType.IsAssignableFrom(actualUnderlyingType))
                    {
                        il.Emit(OpCodes.Box, actualType);
                        il.Emit(OpCodes.Castclass, expectedType);
                    }
                    else if (expectedUnderlyingType.IsAssignableFrom(actualUnderlyingType))
                    {
                        // WARNING: The actual type instance can't be detected at this point, we have to check it at runtime.
                        /*
                         * In this case we emit something like:
                         * {
                         *      if (!(value is [expectedType])) {
                         *          throw new InvalidCastException();
                         *      }
                         *
                         *      return ([expectedType])value;
                         * }
                         */
                        Label lblIsExpected = il.DefineLabel();

                        il.Emit(OpCodes.Box, actualType);
                        il.Emit(OpCodes.Dup);

                        il.Emit(OpCodes.Isinst, expectedType);
                        il.Emit(OpCodes.Brtrue_S, lblIsExpected);

                        il.Emit(OpCodes.Pop);
                        il.ThrowException(typeof(InvalidCastException));

                        il.MarkLabel(lblIsExpected);
                        il.Emit(OpCodes.Castclass, expectedType);
                    }
                    else
                    {
                        throw new InvalidCastException();
                    }
                }
            }
            else
            {
                if (expectedUnderlyingType.IsValueType)
                {
                    // We only allow conversions from objects or interface type if the actual type support this
                    // if not we should throw.
                    if (actualUnderlyingType == typeof(object) || actualUnderlyingType.IsAssignableFrom(expectedUnderlyingType))
                    {
                        // WARNING: The actual type instance can't be detected at this point, we have to check it at runtime.
                        /*
                         * In this case we emit something like:
                         * {
                         *      if (value is null)
                         *      {
                         *          throw new InvalidCastException();
                         *      }
                         *
                         *      if (!(value is [expectedType])) {
                         *          throw new InvalidCastException();
                         *      }
                         *
                         *      return ([expectedType])value;
                         * }
                         */
                        Label lblNotNull = il.DefineLabel();
                        Label lblIsExpected = il.DefineLabel();

                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Brtrue_S, lblNotNull);

                        il.Emit(OpCodes.Pop);
                        il.ThrowException(typeof(InvalidCastException));

                        il.MarkLabel(lblNotNull);
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Isinst, expectedType);
                        il.Emit(OpCodes.Brtrue_S, lblIsExpected);

                        il.Emit(OpCodes.Pop);
                        il.ThrowException(typeof(InvalidCastException));

                        il.MarkLabel(lblIsExpected);
                        il.Emit(OpCodes.Unbox_Any, expectedType);
                    }
                    else
                    {
                        throw new InvalidCastException();
                    }
                }
                else if (expectedUnderlyingType != typeof(object))
                {
                    il.Emit(OpCodes.Castclass, expectedUnderlyingType);
                }
            }
        }

        internal static void WriteGetDuckTypeChaining(ILGenerator il, Type currentType, Type proxyType)
        {
            LocalBuilder instanceLocal = il.DeclareLocal(currentType);
            LocalBuilder createResultLocal = il.DeclareLocal(typeof(DuckType.CreateTypeResult));
            Label isNullLabel = il.DefineLabel();
            Label endLabel = il.DefineLabel();

            // Store the instance stack value to local
            WriteStoreLocal(instanceLocal.LocalIndex, il);

            // Check if the stack value is null
            WriteLoadLocal(instanceLocal.LocalIndex, il);
            il.Emit(OpCodes.Brfalse_S, isNullLabel);

            // ***
            // Is not null, we create the new proxy instance
            // ***

            // Load the proxy definition type to the stack.
            il.Emit(OpCodes.Ldtoken, proxyType);
            il.EmitCall(OpCodes.Call, DuckType.GetTypeFromHandleMethodInfo, null);

            // Load the instance type to the stack.
            WriteLoadLocal(instanceLocal.LocalIndex, il);
            il.EmitCall(OpCodes.Callvirt, DuckType.ObjectGetTypeMethodInfo, null);

            // Gets the CreateTypeResult from calling DuckType.GetOrCreateProxyTypeMethodInfo() to the stack
            // and store it to the local var.
            il.EmitCall(OpCodes.Call, DuckType.GetOrCreateProxyTypeMethodInfo, null);
            WriteStoreLocal(createResultLocal.LocalIndex, il);

            // Load the address of CreateTypeResult (to call an instance method) and load the instance from the local var.
            il.Emit(OpCodes.Ldloca_S, createResultLocal.LocalIndex);
            WriteLoadLocal(instanceLocal.LocalIndex, il);

            // Call DuckType.CreateInstanceMethod() with both values from the stack
            il.EmitCall(OpCodes.Call, DuckType.CreateInstanceMethodInfo, null);

            // Jump and skip the null handling.
            il.Emit(OpCodes.Br, endLabel);

            // ***
            // Is null
            // ***
            il.MarkLabel(isNullLabel);
            il.Emit(OpCodes.Ldnull);

            // End
            il.MarkLabel(endLabel);
        }
    }
}
