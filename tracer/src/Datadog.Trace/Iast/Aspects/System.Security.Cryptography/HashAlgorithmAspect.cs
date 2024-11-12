// <copyright file="HashAlgorithmAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.Security.Cryptography;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.HashAlgorithm;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Aspects;

/// <summary> HashAlgorithm class aspects </summary>
[AspectClass("mscorlib")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class HashAlgorithmAspect
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(HashAlgorithmAspect));

    /// <summary>
    /// ComputeHash not static
    /// </summary>
    /// <param name="target">main HashAlgorithm instance (not static)</param>
    /// <returns>main HashAlgorithm instance</returns>
    [AspectMethodInsertBefore($"System.Security.Cryptography.HashAlgorithm::ComputeHash({ClrNames.ByteArray})", paramShift: 1)]
    [AspectMethodInsertBefore($"System.Security.Cryptography.HashAlgorithm::ComputeHash({ClrNames.ByteArray},{ClrNames.Int32},{ClrNames.Int32})", paramShift: 3)]
    [AspectMethodInsertBefore($"System.Security.Cryptography.HashAlgorithm::ComputeHash({ClrNames.Stream})", paramShift: 1)]
    [AspectMethodInsertBefore($"System.Security.Cryptography.HashAlgorithm::ComputeHashAsync({ClrNames.Stream},{ClrNames.CancellationToken})", paramShift: 2)]
    public static HashAlgorithm ComputeHash(HashAlgorithm target)
    {
        try
        {
            var scope = HashAlgorithmIntegrationCommon.CreateScope(target);
            scope?.Dispose();
            return target;
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HashAlgorithmAspect)}.{nameof(ComputeHash)}");
            return target;
        }
    }
}
#endif
