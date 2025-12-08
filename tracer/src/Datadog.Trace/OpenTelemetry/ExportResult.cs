// <copyright file="ExportResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
#nullable enable

namespace Datadog.Trace.OpenTelemetry
{
    /// <summary>
    /// Enumeration used to define the result of an export operation.
    /// Stub to avoid dependency on OpenTelemetry SDK.
    /// </summary>
    internal enum ExportResult
    {
        /// <summary>
        /// Batch export succeeded.
        /// </summary>
        Success = 0,

        /// <summary>
        /// Batch export failed.
        /// </summary>
        Failure = 1,
    }
}

#endif

