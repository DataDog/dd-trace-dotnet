using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Internal IL Helpers
    /// </summary>
    internal static class ILHelpersExtensions
    {
        private static Func<DynamicMethod, RuntimeMethodHandle> _dynamicGetMethodDescriptor;
        private static List<RuntimeMethodHandle> _handles = new List<RuntimeMethodHandle>();

        static ILHelpersExtensions()
        {
            _dynamicGetMethodDescriptor = (Func<DynamicMethod, RuntimeMethodHandle>)typeof(DynamicMethod)
                .GetMethod("GetMethodDescriptor", BindingFlags.NonPublic | BindingFlags.Instance)
                .CreateDelegate(typeof(Func<DynamicMethod, RuntimeMethodHandle>));
        }

        /// <summary>
        /// Load instance argument
        /// </summary>
        /// <param name="il">ILGenerator</param>
        /// <param name="actualType">Actual type</param>
        /// <param name="expectedType">Expected type</param>
        internal static void LoadInstanceArgument(this ILGenerator il, Type actualType, Type expectedType)
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
        /// <param name="il">IlGenerator</param>
        /// <param name="index">Argument index</param>
        /// <param name="isStatic">Define if we need to take into account the instance argument</param>
        internal static void WriteLoadArgument(this ILGenerator il, int index, bool isStatic)
        {
            if (!isStatic)
            {
                index += 1;
            }

            switch (index)
            {
                case 0:
                    il.Emit(OpCodes.Ldarg_0);
                    break;
                case 1:
                    il.Emit(OpCodes.Ldarg_1);
                    break;
                case 2:
                    il.Emit(OpCodes.Ldarg_2);
                    break;
                case 3:
                    il.Emit(OpCodes.Ldarg_3);
                    break;
                default:
                    il.Emit(OpCodes.Ldarg_S, index);
                    break;
            }
        }

        /// <summary>
        /// Write load local
        /// </summary>
        /// <param name="il">IlGenerator</param>
        /// <param name="index">Local index</param>
        internal static void WriteLoadLocal(this ILGenerator il, int index)
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
        /// <param name="il">IlGenerator</param>
        /// <param name="index">Local index</param>
        internal static void WriteStoreLocal(this ILGenerator il, int index)
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
        /// Convert a current type to an expected type
        /// </summary>
        /// <param name="il">ILGenerator</param>
        /// <param name="actualType">Actual type</param>
        /// <param name="expectedType">Expected type</param>
        internal static void WriteTypeConversion(this ILGenerator il, Type actualType, Type expectedType)
        {
            var actualUnderlyingType = actualType.IsEnum ? Enum.GetUnderlyingType(actualType) : actualType;
            var expectedUnderlyingType = expectedType.IsEnum ? Enum.GetUnderlyingType(expectedType) : expectedType;

            if (actualUnderlyingType == expectedUnderlyingType)
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
        internal static void WriteMethodCalli(this ILGenerator il, MethodInfo method, Type[] methodParameters = null)
        {
            RuntimeMethodHandle handle;

            if (method is DynamicMethod dynMethod)
            {
                // Dynamic methods doesn't expose the internal function pointer
                // so we have to get it using a delegate from reflection.
                handle = _dynamicGetMethodDescriptor(dynMethod);
                lock (_handles)
                {
                    _handles.Add(handle);
                }
            }
            else
            {
                handle = method.MethodHandle;
            }

            il.Emit(OpCodes.Ldc_I8, (long)handle.GetFunctionPointer());
            il.Emit(OpCodes.Conv_I);
            il.EmitCalli(
                OpCodes.Calli,
                method.CallingConvention,
                method.ReturnType,
                methodParameters ?? method.GetParameters().Select(p => p.ParameterType).ToArray(),
                null);
        }
    }
}
