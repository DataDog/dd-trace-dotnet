// <copyright file="DiagnosticsInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading;

namespace Datadog.Trace
{
    /// <summary>
    /// Holds diagnostic metadata readable by external tools (e.g., dd-dotnet crash reporter via ClrMD).
    /// One instance per AppDomain — populated by the tracer at initialization time.
    ///
    /// IMPORTANT: This type is a stable contract for diagnostics consumers. Field names are read
    /// via ClrMD string-based reflection. Do not rename fields without updating all consumers
    /// (dd-dotnet, dd-trace, and associated integration tests).
    /// </summary>
    internal sealed class DiagnosticsInfo
    {
        private static DiagnosticsInfo _instance = null!;
        private static bool _initialized;
        private static object _lock = new();

        private string? _serviceName;
        private string? _runtimeId;

        internal static void Update(string serviceName, string runtimeId)
        {
            var instance = LazyInitializer.EnsureInitialized(ref _instance, ref _initialized, ref _lock);

            if (serviceName is not null)
            {
                instance._serviceName = serviceName;
            }

            if (runtimeId is not null)
            {
                instance._runtimeId = runtimeId;
            }
        }
    }
}
