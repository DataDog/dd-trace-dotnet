// <copyright file="DynamicMethodBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection.Emit;
using Datadog.Trace.ClrProfiler.Emit;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Helper class to instances of <see cref="DynamicMethod"/> using <see cref="System.Reflection.Emit"/>.
    /// </summary>
    /// <typeparam name="TDelegate">The type of delegate</typeparam>
    [Obsolete("This type will be removed in a future version of this library.")]
    public static class DynamicMethodBuilder<TDelegate>
        where TDelegate : Delegate
    {
        /// <summary>
        /// Memoizes CreateMethodCallDelegate
        /// </summary>
        /// <param name="type">The <see cref="Type"/> that contains the method.</param>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="returnType">The method's return type.</param>
        /// <param name="methodParameterTypes">optional types for the method parameters</param>
        /// <param name="methodGenericArguments">optional generic type arguments for a generic method</param>
        /// <returns>A <see cref="Delegate"/> that can be used to execute the dynamic method.</returns>
        public static TDelegate GetOrCreateMethodCallDelegate(
            Type type,
            string methodName,
            Type returnType = null,
            Type[] methodParameterTypes = null,
            Type[] methodGenericArguments = null)
        {
            return Emit.DynamicMethodBuilder<TDelegate>.GetOrCreateMethodCallDelegate(
                type,
                methodName,
                OpCodeValue.Callvirt,
                returnType,
                methodParameterTypes,
                methodGenericArguments);
        }

        /// <summary>
        /// Creates a simple <see cref="DynamicMethod"/> using <see cref="System.Reflection.Emit"/> that
        /// calls a method with the specified name and parameter types.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> that contains the method to call when the returned delegate is executed..</param>
        /// <param name="methodName">The name of the method to call when the returned delegate is executed.</param>
        /// <param name="methodParameterTypes">If not null, use method overload that matches the specified parameters.</param>
        /// <param name="methodGenericArguments">If not null, use method overload that has the same number of generic arguments.</param>
        /// <returns>A <see cref="Delegate"/> that can be used to execute the dynamic method.</returns>
        public static TDelegate CreateMethodCallDelegate(
            Type type,
            string methodName,
            Type[] methodParameterTypes = null,
            Type[] methodGenericArguments = null)
        {
            return Emit.DynamicMethodBuilder<TDelegate>.CreateMethodCallDelegate(
                type,
                methodName,
                OpCodeValue.Callvirt,
                methodParameterTypes,
                methodGenericArguments);
        }
    }
}
