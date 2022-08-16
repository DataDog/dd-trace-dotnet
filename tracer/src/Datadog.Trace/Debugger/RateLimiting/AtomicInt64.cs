// <copyright file="AtomicInt64.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Vendored in from https://github.com/dotnet/dotNext/blob/b77e140ce8291ee8f6d090673efcc045b361559a/src/DotNext/Threading/AtomicInt64.cs
    /// ...because we need "GetAndAccumulate" semantic in order to have a 1-to-1 translation of the Java AdaptiveSampler.
    ///
    /// Various atomic operations for <see cref="long"/> data type
    /// accessible as extension methods.
    /// </summary>
    /// <remarks>
    /// Methods exposed by this class provide volatile read/write
    /// of the field even if it is not declared as volatile field.
    /// </remarks>
    /// <seealso cref="Interlocked"/>
    internal static class AtomicInt64
    {
        /// <summary>
        /// Represents root interface for all functional interfaces.
        /// </summary>
        /// <typeparam name="TDelegate">The type of the delegate representing signature of the functional interface.</typeparam>
        public interface IFunctional<out TDelegate>
            where TDelegate : Delegate
        {
            /// <summary>
            /// Converts functional object to the delegate.
            /// </summary>
            /// <returns>The delegate representing this functional object.</returns>
            TDelegate ToDelegate();
        }

        /// <summary>
        /// Represents functional interface returning arbitrary value and
        /// accepting the two arguments.
        /// </summary>
        /// <remarks>
        /// Functional interface can be used to pass
        /// some application logic without heap allocation in
        /// contrast to regulat delegates. Additionally, implementation
        /// of functional interface may have encapsulated data acting
        /// as closure which is not allocated on the heap.
        /// </remarks>
        /// <typeparam name="T1">The type of the first argument.</typeparam>
        /// <typeparam name="T2">The type of the second argument.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        public interface ISupplier<in T1, in T2, out TResult> : IFunctional<Func<T1, T2, TResult>>
        {
            /// <summary>
            /// Invokes the supplier.
            /// </summary>
            /// <param name="arg1">The first argument.</param>
            /// <param name="arg2">The second argument.</param>
            /// <returns>The value returned by this supplier.</returns>
            TResult Invoke(T1 arg1, T2 arg2);
        }

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the original value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetAndAccumulate(ref this long value, long x, Func<long, long, long> accumulator)
        {
            return Accumulate<DelegatingSupplier<long, long, long>>(ref value, x, accumulator);
        }

        private static long Accumulate<TAccumulator>(ref long value, long x, TAccumulator accumulator)
            where TAccumulator : struct, ISupplier<long, long, long>
        {
            long oldValue, newValue, tmp = Volatile.Read(ref value);
            do
            {
                newValue = accumulator.Invoke(oldValue = tmp, x);
            }
            while ((tmp = Interlocked.CompareExchange(ref value, newValue, oldValue)) != oldValue);

            return oldValue;
        }

        /// <summary>
        /// Represents implementation of <see cref="ISupplier{T1, T2, TResult}"/> that delegates
        /// invocation to the delegate of type <see cref="Func{T1, T2, TResult}"/>.
        /// </summary>
        /// <typeparam name="T1">The type of the first argument.</typeparam>
        /// <typeparam name="T2">The type of the second argument.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct DelegatingSupplier<T1, T2, TResult> : ISupplier<T1, T2, TResult>, IEquatable<DelegatingSupplier<T1, T2, TResult>>
        {
            private readonly Func<T1, T2, TResult> func;

            /// <summary>
            /// Initializes a new instance of the <see cref="DelegatingSupplier{T1, T2, TResult}"/> struct.
            /// Wraps the delegate instance.
            /// </summary>
            /// <param name="func">The delegate instance.</param>
            /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
            public DelegatingSupplier(Func<T1, T2, TResult> func)
            {
                this.func = func ?? throw new ArgumentNullException(nameof(func));
            }

            /// <summary>
            /// Gets a value indicating whether that the underlying delegate is <see langword="null"/>.
            /// </summary>
            public bool IsEmpty => func is null;

            /// <summary>
            /// Wraps the delegate instance.
            /// </summary>
            /// <param name="func">The delegate instance.</param>
            /// <returns>The supplier represented by the delegate.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
            public static implicit operator DelegatingSupplier<T1, T2, TResult>(Func<T1, T2, TResult> func)
            {
                return new(func);
            }

            /// <summary>
            /// Determines whether the two objects contain references to the same delegate instance.
            /// </summary>
            /// <param name="x">The first object to compare.</param>
            /// <param name="y">The second object to compare.</param>
            /// <returns><see langword="true"/> if the both objects contain references the same delegate instance; otherwise, <see langword="false"/>.</returns>
            public static bool operator ==(DelegatingSupplier<T1, T2, TResult> x, DelegatingSupplier<T1, T2, TResult> y)
            {
                return x.Equals(y);
            }

            /// <summary>
            /// Determines whether the two objects contain references to the different delegate instances.
            /// </summary>
            /// <param name="x">The first object to compare.</param>
            /// <param name="y">The second object to compare.</param>
            /// <returns><see langword="true"/> if the both objects contain references the different delegate instances; otherwise, <see langword="false"/>.</returns>
            public static bool operator !=(DelegatingSupplier<T1, T2, TResult> x, DelegatingSupplier<T1, T2, TResult> y)
            {
                return !x.Equals(y);
            }

            /// <inheritdoc />
            TResult ISupplier<T1, T2, TResult>.Invoke(T1 arg1, T2 arg2)
            {
                return func(arg1, arg2);
            }

            /// <inheritdoc />
            Func<T1, T2, TResult> IFunctional<Func<T1, T2, TResult>>.ToDelegate()
            {
                return func;
            }

            /// <summary>
            /// Determines whether this object contains the same delegate instance as the specified object.
            /// </summary>
            /// <param name="other">The object to compare.</param>
            /// <returns><see langword="true"/> if this object contains the same delegate instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
            public bool Equals(DelegatingSupplier<T1, T2, TResult> other)
            {
                return ReferenceEquals(func, other.func);
            }

            /// <summary>
            /// Determines whether this object contains the same delegate instance as the specified object.
            /// </summary>
            /// <param name="other">The object to compare.</param>
            /// <returns><see langword="true"/> if this object contains the same delegate instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
            public override bool Equals(object other)
            {
                return other is DelegatingSupplier<T1, T2, TResult> supplier && Equals(supplier);
            }

            /// <summary>
            /// Gets the hash code representing identity of the stored delegate instance.
            /// </summary>
            /// <returns>The hash code representing identity of the stored delegate instance.</returns>
            public override int GetHashCode()
            {
                return RuntimeHelpers.GetHashCode(func);
            }
        }
    }
}
