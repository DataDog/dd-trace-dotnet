//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
#nullable enable
namespace Datadog.Trace.Vendors.Newtonsoft.Json
{
    /// <summary>
    /// Provides an interface for using pooled arrays.
    /// </summary>
    /// <typeparam name="T">The array type content.</typeparam>
    internal interface IArrayPool<T>
    {
        /// <summary>
        /// Rent an array from the pool. This array must be returned when it is no longer needed.
        /// </summary>
        /// <param name="minimumLength">The minimum required length of the array. The returned array may be longer.</param>
        /// <returns>The rented array from the pool. This array must be returned when it is no longer needed.</returns>
        T[] Rent(int minimumLength);

        /// <summary>
        /// Return an array to the pool.
        /// </summary>
        /// <param name="array">The array that is being returned.</param>
        void Return(T[]? array);
    }
}