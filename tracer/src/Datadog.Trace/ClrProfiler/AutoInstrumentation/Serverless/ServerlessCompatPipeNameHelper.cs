// <copyright file="ServerlessCompatPipeNameHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETCOREAPP
using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Serverless
{
    /// <summary>
    /// Helper class for generating unique pipe names for serverless compat layer coordination.
    /// Shared logic for both trace and metrics pipe name generation.
    /// </summary>
    internal static class ServerlessCompatPipeNameHelper
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServerlessCompatPipeNameHelper));

        /// <summary>
        /// Generates a unique pipe name by appending a GUID to the base name.
        /// Validates and truncates the base name if necessary to ensure the full pipe path stays within Windows limits.
        /// </summary>
        /// <param name="compatLayerBaseName">The base name calculated by the compat layer, or null</param>
        /// <param name="defaultBaseName">The default base name to use if compat layer returns null</param>
        /// <param name="pipeType">The type of pipe for logging (e.g., "trace" or "DogStatsD")</param>
        /// <returns>A unique pipe name in the format {base}_{guid}</returns>
        internal static string GenerateUniquePipeName(string? compatLayerBaseName, string defaultBaseName, string pipeType)
        {
            var baseName = compatLayerBaseName ?? defaultBaseName;

            // Validate base pipe name length before appending GUID
            // Windows pipe path format: \\.\pipe\{base}_{guid}
            // Max total: 256 - 9 (\\.\pipe\) - 1 (underscore) - 32 (GUID) = 214
            const int maxBaseLength = 214;

            if (baseName.Length > maxBaseLength)
            {
                Log.Warning<string, int, int>("{PipeType} pipe base name exceeds {MaxLength} characters ({ActualLength}). Truncating to allow for GUID suffix.", pipeType, maxBaseLength, baseName.Length);
                baseName = baseName.Substring(0, maxBaseLength);
            }

            var guid = Guid.NewGuid().ToString("N"); // "N" format removes hyphens (32 chars)
            var uniqueName = $"{baseName}_{guid}";

            Log.Information("ServerlessCompat integration: Generated unique {PipeType} pipe name: {PipeName}", pipeType, uniqueName);
            return uniqueName;
        }
    }
}
#endif
