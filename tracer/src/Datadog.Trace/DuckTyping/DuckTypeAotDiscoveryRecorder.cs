// <copyright file="DuckTypeAotDiscoveryRecorder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Records dynamic duck typing mapping requests into a ducktype-aot map file when explicitly enabled.
    /// This is intended for parity and migration testing workflows only.
    /// </summary>
    internal static class DuckTypeAotDiscoveryRecorder
    {
        /// <summary>
        /// Stores output path.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private static readonly string? OutputPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.DuckTypeAotDiscoveryOutputPath);

        /// <summary>
        /// Stores cached mappings data.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private static readonly ConcurrentDictionary<string, MapEntry> Mappings = new(StringComparer.Ordinal);

        /// <summary>
        /// Stores process exit hook registered.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private static int _processExitHookRegistered;

        /// <summary>
        /// Executes record.
        /// </summary>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="reverse">The reverse value.</param>
        internal static void Record(Type proxyType, Type targetType, bool reverse)
        {
            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                return;
            }

            if (proxyType is null || targetType is null)
            {
                return;
            }

            var proxyTypeName = proxyType.FullName;
            var targetTypeName = targetType.FullName;
            var proxyAssembly = proxyType.Assembly.GetName().Name;
            var targetAssembly = targetType.Assembly.GetName().Name;

            if (string.IsNullOrWhiteSpace(proxyTypeName) ||
                string.IsNullOrWhiteSpace(targetTypeName) ||
                string.IsNullOrWhiteSpace(proxyAssembly) ||
                string.IsNullOrWhiteSpace(targetAssembly))
            {
                return;
            }

            EnsureProcessExitHook();

            var mode = reverse ? "reverse" : "forward";
            var key = string.Concat(mode, "|", proxyTypeName, "|", proxyAssembly, "|", targetTypeName, "|", targetAssembly);
            _ = Mappings.TryAdd(
                key,
                new MapEntry
                {
                    Mode = mode,
                    ProxyType = proxyTypeName,
                    ProxyAssembly = proxyAssembly,
                    TargetType = targetTypeName,
                    TargetAssembly = targetAssembly
                });
        }

        /// <summary>
        /// Ensures ensure process exit hook.
        /// </summary>
        private static void EnsureProcessExitHook()
        {
            if (Interlocked.CompareExchange(ref _processExitHookRegistered, 1, 0) == 0)
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
            }
        }

        /// <summary>
        /// Executes flush.
        /// </summary>
        private static void Flush()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(OutputPath))
                {
                    return;
                }

                var directory = Path.GetDirectoryName(OutputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var document = new MapDocument
                {
                    Mappings = Mappings
                              .Values
                              .OrderBy(mapping => mapping.Mode, StringComparer.Ordinal)
                              .ThenBy(mapping => mapping.ProxyAssembly, StringComparer.Ordinal)
                              .ThenBy(mapping => mapping.ProxyType, StringComparer.Ordinal)
                              .ThenBy(mapping => mapping.TargetAssembly, StringComparer.Ordinal)
                              .ThenBy(mapping => mapping.TargetType, StringComparer.Ordinal)
                              .ToList()
                };

                var json = JsonConvert.SerializeObject(document, Formatting.Indented);
                File.WriteAllText(OutputPath, json);
            }
            catch
            {
                // Best effort recorder used only for testing workflows.
            }
        }

        /// <summary>
        /// Represents map document.
        /// </summary>
        private sealed class MapDocument
        {
            /// <summary>
            /// Gets or sets mappings.
            /// </summary>
            /// <value>The mappings value.</value>
            [JsonProperty("mappings")]
            public List<MapEntry> Mappings { get; set; } = new();
        }

        /// <summary>
        /// Represents map entry.
        /// </summary>
        private sealed class MapEntry
        {
            /// <summary>
            /// Gets or sets mode.
            /// </summary>
            /// <value>The mode value.</value>
            [JsonProperty("mode")]
            public string Mode { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets proxy type.
            /// </summary>
            /// <value>The proxy type value.</value>
            [JsonProperty("proxyType")]
            public string ProxyType { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets proxy assembly.
            /// </summary>
            /// <value>The proxy assembly value.</value>
            [JsonProperty("proxyAssembly")]
            public string ProxyAssembly { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets target type.
            /// </summary>
            /// <value>The target type value.</value>
            [JsonProperty("targetType")]
            public string TargetType { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets target assembly.
            /// </summary>
            /// <value>The target assembly value.</value>
            [JsonProperty("targetAssembly")]
            public string TargetAssembly { get; set; } = string.Empty;
        }
    }
}
