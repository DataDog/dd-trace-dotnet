// <copyright file="ExceptionHashProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Util;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    /// <summary>
    /// Computes the exception_hash reported with Exception Replay spans and snapshots.
    /// The value must be stable across processes and runtimes, and must not depend on OS cryptography (FIPS-enabled hosts block MD5).
    /// </summary>
    internal static class ExceptionHashProvider
    {
        private const FnvHash64.Version HashVersion = FnvHash64.Version.V1A;

        internal static string GetHash(ExceptionIdentifier exceptionId)
        {
            var hash = HashChars(((byte)exceptionId.ErrorOrigin).ToString(CultureInfo.InvariantCulture), FnvHash64.Empty);

            foreach (var exceptionType in exceptionId.ExceptionTypes)
            {
                if (exceptionType.FullName is { } fullName)
                {
                    hash = HashChars(fullName, hash);
                }
            }

            foreach (var frame in exceptionId.StackTrace)
            {
                hash = HashChars(frame.Method.GetFullyQualifiedName() ?? frame.Method.Name, hash);
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        // Hashes the string's in-memory UTF-16 bytes, because transcoding to UTF-8 allocates a byte[] per string.
        // Those bytes depend on CPU byte order, which is little-endian on every platform the tracer supports, so all hosts get the same hash.
        private static ulong HashChars(string value, ulong hash)
            => FnvHash64.GenerateHash(MemoryMarshal.AsBytes(value.AsSpan()), HashVersion, hash);
    }
}
