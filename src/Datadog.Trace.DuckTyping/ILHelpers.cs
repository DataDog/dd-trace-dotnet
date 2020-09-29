using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Internal IL Helpers
    /// </summary>
    internal static class ILHelpers
    {
        private static Func<DynamicMethod, RuntimeMethodHandle> _dynamicGetMethodDescriptor;

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
            var actualUnderlyingType = actualType.IsEnum ? Enum.GetUnderlyingType(actualType) : actualType;
            var expectedUnderlyingType = expectedType.IsEnum ? Enum.GetUnderlyingType(expectedType) : expectedType;

            if (actualUnderlyingType == expectedUnderlyingType)
            {
                return;
            }

            if (actualUnderlyingType.IsGenericParameter && expectedUnderlyingType.IsGenericParameter)
            {
                return;
            }

            if (actualUnderlyingType.IsValueType)
            {
                if (expectedUnderlyingType.IsValueType)
                {
                    // If both underlying types are value types then both must be of the same type.
                    DuckTypeInvalidTypeConversionException.Throw(actualType, expectedType);
                }
                else
                {
                    // An underlying type can be boxed and converted to an object or interface type if the actual type support this
                    // if not we should throw.
                    if (expectedUnderlyingType == typeof(object))
                    {
                        // If the expected type is object we just need to box the value
                        il.Emit(OpCodes.Box, actualType);
                    }
                    else if (expectedUnderlyingType.IsAssignableFrom(actualUnderlyingType))
                    {
                        // If the expected type can be assigned from the value type (ex: struct implementing an interface)
                        il.Emit(OpCodes.Box, actualType);
                        il.Emit(OpCodes.Castclass, expectedType);
                    }
                    else
                    {
                        // If the expected type can't be assigned from the actual value type.
                        // Means if the expected type is an interface the actual type doesn't implement it.
                        // So no possible conversion or casting can be made here.
                        DuckTypeInvalidTypeConversionException.Throw(actualType, expectedType);
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
                         *      if (!(value is [expectedType])) {
                         *          throw new InvalidCastException();
                         *      }
                         *
                         *      return ([expectedType])value;
                         * }
                         */
                        Label lblIsExpected = il.DefineLabel();

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
                        DuckTypeInvalidTypeConversionException.Throw(actualType, expectedType);
                    }
                }
                else if (expectedUnderlyingType != typeof(object))
                {
                    il.Emit(OpCodes.Castclass, expectedUnderlyingType);
                }
            }
        }

        /// <summary>
        /// Write a Call to a method using Calli
        /// </summary>
        /// <param name="il">ILGenerator</param>
        /// <param name="method">Method to get called</param>
        /// <param name="methodParameters">Method parameters (to avoid the allocations of calculating it)</param>
        internal static void WriteMethodCalli(ILGenerator il, MethodInfo method, Type[] methodParameters = null)
        {
            long fnPointer = 0;
            if (method is DynamicMethod dynMethod)
            {
                // Dynamic methods doesn't expose the internal function pointer
                // so we have to get it using a delegate from reflection.
                fnPointer = (long)GetRuntimeHandle(dynMethod).GetFunctionPointer();
            }
            else
            {
                fnPointer = (long)method.MethodHandle.GetFunctionPointer();
            }

            il.Emit(OpCodes.Ldc_I8, fnPointer);
            il.Emit(OpCodes.Conv_I);
            il.EmitCalli(
                OpCodes.Calli,
                method.CallingConvention,
                method.ReturnType,
                methodParameters ?? method.GetParameters().Select(p => p.ParameterType).ToArray(),
                null);
        }

        private static RuntimeMethodHandle GetRuntimeHandle(DynamicMethod dynamicMethod)
        {
            _dynamicGetMethodDescriptor ??= (Func<DynamicMethod, RuntimeMethodHandle>)typeof(DynamicMethod)
                .GetMethod("GetMethodDescriptor", BindingFlags.NonPublic | BindingFlags.Instance)
                .CreateDelegate(typeof(Func<DynamicMethod, RuntimeMethodHandle>));
            return _dynamicGetMethodDescriptor(dynamicMethod);
        }
    }
}
