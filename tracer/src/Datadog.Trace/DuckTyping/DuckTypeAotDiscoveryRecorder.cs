// <copyright file="DuckTypeAotDiscoveryRecorder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
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
        private static readonly object FlushLock = new();

        /// <summary>
        /// How long a flush waits for another process holding the output lock file.
        /// </summary>
        private static readonly TimeSpan OutputLockTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Stores process exit hook registered.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private static int _processExitHookRegistered;
        private static int _recordsSinceLastFlush;

        /// <summary>
        /// Executes record.
        /// </summary>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="reverse">The reverse value.</param>
        internal static void Record(Type proxyType, Type targetType, bool reverse)
        {
            // Branch: take this path when (StringUtil.IsNullOrWhiteSpace(OutputPath)) evaluates to true.
            if (StringUtil.IsNullOrWhiteSpace(OutputPath))
            {
                return;
            }

            // Branch: take this path when (proxyType is null || targetType is null) evaluates to true.
            if (proxyType is null || targetType is null)
            {
                return;
            }

            var proxyTypeName = proxyType.FullName;
            var targetTypeName = targetType.FullName;
            var proxyAssembly = proxyType.Assembly.GetName().Name;
            var targetAssembly = targetType.Assembly.GetName().Name;

            // Branch: take this path when (StringUtil.IsNullOrWhiteSpace(proxyTypeName) || evaluates to true.
            if (StringUtil.IsNullOrWhiteSpace(proxyTypeName) ||
                StringUtil.IsNullOrWhiteSpace(targetTypeName) ||
                StringUtil.IsNullOrWhiteSpace(proxyAssembly) ||
                StringUtil.IsNullOrWhiteSpace(targetAssembly))
            {
                return;
            }

            EnsureProcessExitHook();

            var mapEntry = new MapEntry
            {
                Mode = reverse ? "reverse" : "forward",
                ProxyType = proxyTypeName,
                ProxyAssembly = proxyAssembly,
                TargetType = targetTypeName,
                TargetAssembly = targetAssembly
            };
            if (!Mappings.TryAdd(GetKey(mapEntry), mapEntry))
            {
                return;
            }

            // A generated class proxy is also a target for forward duck casts. Preserve its
            // stable base contract so generation can register the corresponding AOT proxy type.
            if (!reverse && targetType.Assembly.IsDynamic &&
                typeof(IDuckType).IsAssignableFrom(targetType) &&
                targetType.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType))
            {
                Record(proxyType, baseType, reverse: false);
            }

            if ((Interlocked.Increment(ref _recordsSinceLastFlush) % 256) == 0)
            {
                Flush();
            }
        }

        /// <summary>
        /// Ensures ensure process exit hook.
        /// </summary>
        private static void EnsureProcessExitHook()
        {
            // Branch: take this path when (Interlocked.CompareExchange(ref _processExitHookRegistered, 1, 0) == 0) evaluates to true.
            if (Interlocked.CompareExchange(ref _processExitHookRegistered, 1, 0) == 0)
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
                AppDomain.CurrentDomain.DomainUnload += (_, _) => Flush();
            }
        }

        /// <summary>
        /// Persists recorded mappings before a controlled shutdown.
        /// </summary>
        internal static void Flush()
        {
            try
            {
                // Branch: take this path when (StringUtil.IsNullOrWhiteSpace(OutputPath)) evaluates to true.
                if (StringUtil.IsNullOrWhiteSpace(OutputPath))
                {
                    return;
                }

                var directory = Path.GetDirectoryName(OutputPath);
                // Branch: take this path when (!StringUtil.IsNullOrWhiteSpace(directory)) evaluates to true.
                if (!StringUtil.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                lock (FlushLock)
                {
                    // Child processes (e.g. one testhost per target framework) inherit the output path, so several
                    // processes can flush to the same file. Serialize them with a lock file and merge with whatever
                    // the other processes already wrote instead of overwriting their mappings.
                    using var outputLock = AcquireOutputLock(OutputPath);

                    var mappings = new Dictionary<string, MapEntry>(StringComparer.Ordinal);
                    foreach (var existingMapping in ReadExistingMappings(OutputPath))
                    {
                        mappings[GetKey(existingMapping)] = existingMapping;
                    }

                    foreach (var mapping in Mappings)
                    {
                        mappings[mapping.Key] = mapping.Value;
                    }

                    var document = new MapDocument
                    {
                        Mappings = mappings
                                  .Values
                                  .OrderBy(mapping => mapping.Mode, StringComparer.Ordinal)
                                  .ThenBy(mapping => mapping.ProxyAssembly, StringComparer.Ordinal)
                                  .ThenBy(mapping => mapping.ProxyType, StringComparer.Ordinal)
                                  .ThenBy(mapping => mapping.TargetAssembly, StringComparer.Ordinal)
                                  .ThenBy(mapping => mapping.TargetType, StringComparer.Ordinal)
                                  .ToList()
                    };

                    var json = JsonHelper.SerializeObject(document, new JsonSerializerSettings { Formatting = Formatting.Indented });
                    // Unique staging file, in case the lock couldn't be acquired and another process writes concurrently.
                    var temporaryOutputPath = $"{OutputPath}.{Guid.NewGuid():N}.tmp";
                    File.WriteAllText(temporaryOutputPath, json);
                    File.Copy(temporaryOutputPath, OutputPath, overwrite: true);
                    File.Delete(temporaryOutputPath);
                    _ = Interlocked.Exchange(ref _recordsSinceLastFlush, 0);
                }
            }
            catch
            {
                // Branch: handles any exception that reaches this handler.
                // Best effort recorder used only for testing workflows.
            }
        }

        private static string GetKey(MapEntry mapping)
        {
            return string.Concat(mapping.Mode, "|", mapping.ProxyType, "|", mapping.ProxyAssembly, "|", mapping.TargetType, "|", mapping.TargetAssembly);
        }

        private static FileStream? AcquireOutputLock(string outputPath)
        {
            // The lock file is intentionally left in place: deleting it on release would let two processes
            // lock different files with the same path.
            var lockPath = outputPath + ".lock";
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException) when (stopwatch.Elapsed < OutputLockTimeout)
                {
                    Thread.Sleep(10);
                }
                catch (IOException)
                {
                    // Best effort: write without the lock rather than dropping this process' mappings.
                    return null;
                }
            }
        }

        private static List<MapEntry> ReadExistingMappings(string outputPath)
        {
            try
            {
                if (File.Exists(outputPath) &&
                    JsonHelper.DeserializeObject<MapDocument>(File.ReadAllText(outputPath)) is { Mappings: { } existingMappings })
                {
                    return existingMappings;
                }
            }
            catch
            {
                // An unreadable map (e.g. written by a process that crashed mid-write) is replaced by ours.
            }

            return [];
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
