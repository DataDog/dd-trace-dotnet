// <copyright file="IRandomIdGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Util;

/// <summary>
/// This interface is not used at run time. It is here only to ensure that the different implementations
/// with different target frameworks expose the same API.
/// </summary>
internal interface IRandomIdGenerator
{
    /// <summary>
    /// Returns a random unsigned 64-bit number that is greater than zero.
    /// If <paramref name="useAllBits"/> is <c>false</c> (default),
    /// the number is less than or equal to Int64.MaxValue (0x7fffffffffffffff). This is the default mode (aka uint63)
    /// and is used for backwards compatibility with tracers that parse ids as signed integers.
    /// Otherwise, it is less than or equal to UInt64.MaxValue (0xffffffffffffffff).
    /// </summary>
    ulong NextSpanId(bool useAllBits);

    /// <summary>
    /// Returns a random unsigned 64-bit number that is greater than zero.
    /// If <paramref name="useAllBits"/> is <c>false</c> (default),
    /// the number is less than or equal to Int64.MaxValue (0x7fffffffffffffff). This is the default mode (aka uint63)
    /// and is used for backwards compatibility with tracers that parse ids as signed integers.
    /// Otherwise, it is less than or equal to Int128.MaxValue (0xffffffffffffffffffffffffffffffff).
    /// </summary>
    TraceId NextTraceId(bool useAllBits);
}
