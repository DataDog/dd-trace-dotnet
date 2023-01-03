// <copyright file="RuntimeHandleTuple.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.TraceAnnotations
{
    internal readonly struct RuntimeHandleTuple : IEquatable<RuntimeHandleTuple>
    {
        /// <summary>
        /// The method handle
        /// </summary>
        public readonly RuntimeMethodHandle MethodHandle;

        /// <summary>
        /// The type handle
        /// </summary>
        public readonly RuntimeTypeHandle TypeHandle;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeHandleTuple"/> struct.
        /// </summary>
        /// <param name="methodHandle">The method handle</param>
        /// <param name="typeHandle">The owning type handle</param>
        public RuntimeHandleTuple(RuntimeMethodHandle methodHandle, RuntimeTypeHandle typeHandle)
        {
            MethodHandle = methodHandle;
            TypeHandle = typeHandle;
        }

        /// <summary>
        /// Gets the struct hashcode
        /// </summary>
        /// <returns>Hashcode</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(MethodHandle, TypeHandle);
        }

        /// <summary>
        /// Gets if the struct is equal to other object or struct
        /// </summary>
        /// <param name="obj">Object to compare</param>
        /// <returns>True if both are equals; otherwise, false.</returns>
        public override bool Equals(object? obj)
        {
            return obj is RuntimeHandleTuple vTuple &&
                   MethodHandle.Equals(vTuple.MethodHandle) &&
                   TypeHandle.Equals(vTuple.TypeHandle);
        }

        /// <inheritdoc />
        public bool Equals(RuntimeHandleTuple other)
        {
            return MethodHandle.Equals(other.MethodHandle) &&
                   TypeHandle.Equals(other.TypeHandle);
        }
    }
}
