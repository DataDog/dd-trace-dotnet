// <copyright file="InstrumentationAllocator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// Used by instrumentation code to allocate objects on the heap.
    /// It's main purpose is to act as a layer that enables reuse of objects using techniques such as ArrayPools.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class InstrumentationAllocator
    {
        /// <summary>
        /// Allocates an array on the heap using an Object Pooling.
        /// </summary>
        /// <typeparam name="T">Type of the array to rent.</typeparam>
        /// <param name="size">Number of elements to rent.</param>
        /// <returns>Rented array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] RentArray<T>(int size)
        {
            return new T[size];
        }

        /// <summary>
        /// Returns rented array.
        /// </summary>
        /// <typeparam name="T">The type of the rented array.</typeparam>
        /// <param name="rentedArray">The rented array to return.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnArray<T>(ref T[] rentedArray)
        {
            // TODO
        }

        /// <summary>
        /// Allocates object on the heap using an Object Pooling.
        /// </summary>
        /// <typeparam name="T">Type of the object to rent.</typeparam>
        /// <returns>Rented object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AllocateObject<T>()
            where T : new()
        {
            return new T();
        }

        /// <summary>
        /// Returns rented array.
        /// </summary>
        /// <typeparam name="T">The type of the rented object.</typeparam>
        /// <param name="rentedObject">The rented object to return.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnObject<T>(ref T rentedObject)
        {
            // TODO
        }
    }
}
