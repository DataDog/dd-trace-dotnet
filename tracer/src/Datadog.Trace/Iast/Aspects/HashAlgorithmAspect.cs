// <copyright file="HashAlgorithmAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.HashAlgorithm;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects
{
    /// <summary> HashAlgorithm class aspects </summary>
    [AspectClass("mscorlib,System.Security.Cryptography.Primitives,System.Security.Cryptography")]
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public partial class HashAlgorithmAspect
    {
        /// <summary>
        /// ComputeHash not static
        /// </summary>
        /// <param name="target">main HashAlgorithm instance (not static)</param>
        /// <returns>main HashAlgorithm instance</returns>
        [AspectMethodInsertBefore($"System.Security.Cryptography.HashAlgorithm::ComputeHash({ClrNames.ByteArray})", paramShift: 1)]
        [AspectMethodInsertBefore($"System.Security.Cryptography.HashAlgorithm::ComputeHash({ClrNames.ByteArray},{ClrNames.Int32},{ClrNames.Int32})", paramShift: 3)]
        [AspectMethodInsertBefore($"System.Security.Cryptography.HashAlgorithm::ComputeHash({ClrNames.Stream})", paramShift: 1)]
        public static HashAlgorithm ComputeHash(HashAlgorithm target)
        {
            var scope = HashAlgorithmIntegrationCommon.CreateScope(target);
            scope?.Dispose();
            return target;
        }

        /// <summary>
        /// ComputeHash not static
        /// </summary>
        /// <param name="target">main HashAlgorithm instance (not static)</param>
        /// <returns>main HashAlgorithm instance</returns>
        [AspectMethodInsertBefore($"System.Security.Cryptography.HashAlgorithm::ComputeHashAsync({ClrNames.Stream},{ClrNames.CancellationToken})", paramShift: 2)]
        public static HashAlgorithm ComputeHashAsync(HashAlgorithm target)
        {
            var scope = HashAlgorithmIntegrationCommon.CreateScope(target);
            scope?.Dispose();
            return target;
        }
    }
}
