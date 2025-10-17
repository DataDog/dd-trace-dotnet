// <copyright file="Guard.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if NETCOREAPP3_1_OR_GREATER

using System;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// Stub for OpenTelemetry's Guard utility class.
    /// Used by vendored gRPC client for parameter validation.
    /// </summary>
    internal static class Guard
    {
        public static void ThrowIfNull(object? value, string? paramName = null)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }
}
#endif
