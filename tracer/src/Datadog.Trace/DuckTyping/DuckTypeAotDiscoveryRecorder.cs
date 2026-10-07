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
using System.Text;
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
        /// Number of new mappings after which they are flushed in the background, so a process that doesn't
        /// shut down cleanly still leaves most of its mappings behind.
        /// </summary>
        private const int PeriodicFlushThreshold = 256;

        /// <summary>
        /// Stores output path.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private static readonly string? OutputPath = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.DuckTypeAotDiscoveryOutputPath);

        /// <summary>
        /// How long an explicit flush waits for another process holding the output lock file.
        /// Periodic flushes don't wait: a later flush picks their mappings up.
        /// </summary>
        private static readonly TimeSpan ExplicitFlushLockTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Stores process exit hook registered.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        private static int _processExitHookRegistered;

        /// <summary>
        /// Number of mappings recorded since the last successful flush.
        /// </summary>
        private static int _pendingMappings;

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

            try
            {
                RecordCore(proxyType, targetType, reverse);
            }
            catch
            {
                // Recording is best effort: it runs while a proxy type is created, and must never make that creation fail.
            }
        }

        private static void RecordCore(Type proxyType, Type targetType, bool reverse)
        {
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
            if (!RecorderState.Mappings.TryAdd(GetKey(mapEntry), mapEntry))
            {
                return;
            }

            // A generated proxy is also a target for forward duck casts: record the stable type the registry serves it with. For a
            // reverse proxy, the type it was created for (a class it derives from or an interface it implements); otherwise the
            // base class of a generated class proxy.
            if (!reverse && targetType.Assembly.IsDynamic &&
                typeof(IDuckType).IsAssignableFrom(targetType) &&
                (DuckType.GetDynamicReverseProxyDefinitionType(targetType) ??
                 (targetType.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType) ? baseType : null)) is { } stableTargetType)
            {
                Record(proxyType, stableTargetType, reverse: false);
            }

            // Merging into the shared map rereads it, so don't do it on the thread that is creating a proxy.
            if ((Interlocked.Increment(ref _pendingMappings) % PeriodicFlushThreshold) == 0)
            {
                ThreadPool.UnsafeQueueUserWorkItem(static _ => Flush(isExplicit: false), null);
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
        /// Gets the path of the file a map path designates: a symbolic link keeps pointing to the map, the file it links to is
        /// the one written (and locked). Symbolic links are followed on .NET 6 and later only.
        /// </summary>
        /// <param name="path">The map path.</param>
        /// <returns>The path of the file to write.</returns>
        internal static string ResolveMapPath(string path)
        {
#if NET6_0_OR_GREATER
            try
            {
                return File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path;
            }
            catch (IOException)
            {
                // A broken link: written like a file.
            }
#endif
            return path;
        }

        /// <summary>
        /// Persists recorded mappings before a controlled shutdown, merging them into the map that other processes
        /// sharing the output path already wrote.
        /// </summary>
        internal static void Flush() => Flush(isExplicit: true);

        private static void Flush(bool isExplicit)
        {
            var outputPath = OutputPath;
            // Branch: take this path when (StringUtil.IsNullOrWhiteSpace(outputPath)) evaluates to true.
            if (StringUtil.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            // A periodic flush is skipped while another flush of this process is running; an explicit one waits for it.
            if (!Monitor.TryEnter(RecorderState.FlushLock, isExplicit ? Timeout.Infinite : 0))
            {
                return;
            }

            try
            {
                var pendingMappings = Volatile.Read(ref _pendingMappings);
                // Branch: nothing was recorded since the last flush (e.g. the exit flush after the test framework one).
                if (pendingMappings == 0)
                {
                    return;
                }

                var directory = Path.GetDirectoryName(outputPath);
                // Branch: take this path when (!StringUtil.IsNullOrWhiteSpace(directory)) evaluates to true.
                if (!StringUtil.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Child processes (e.g. one testhost per target framework) inherit the output path. A lock file
                // serializes the read-merge-write of every process so none of them drops the others' mappings.
                using var outputLock = AcquireOutputLock(outputPath, isExplicit ? ExplicitFlushLockTimeout : TimeSpan.Zero);
                // Branch: a periodic flush doesn't wait for the lock. An explicit flush only gets here without the lock
                // when it can't be acquired at all (timeout, or a lock file this process can't open) and writes anyway.
                if (outputLock is null && !isExplicit)
                {
                    return;
                }

                // Branch: never overwrite a map that couldn't be read; a later flush retries.
                if (!TryReadExistingMappings(outputPath, out var existingMappings))
                {
                    return;
                }

                var mappings = new Dictionary<string, MapEntry>(StringComparer.Ordinal);
                foreach (var existingMapping in existingMappings)
                {
                    mappings[GetKey(existingMapping)] = existingMapping;
                }

                foreach (var mapping in RecorderState.Mappings)
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

                WriteAtomically(outputPath, JsonHelper.SerializeObject(document, new JsonSerializerSettings { Formatting = Formatting.Indented }));

                // Branch: a write without the lock may still be overwritten by a process that read the map before it, so
                // keep the mappings pending and let the next flush (e.g. the exit one) write them again.
                if (outputLock is not null)
                {
                    _ = Interlocked.Add(ref _pendingMappings, -pendingMappings);
                }
            }
            catch
            {
                // Branch: handles any exception that reaches this handler.
                // Best effort recorder used only for testing workflows; the mappings stay pending for a later flush.
            }
            finally
            {
                Monitor.Exit(RecorderState.FlushLock);
            }
        }

        private static string GetKey(MapEntry mapping)
        {
            return string.Concat(mapping.Mode, "|", mapping.ProxyType, "|", mapping.ProxyAssembly, "|", mapping.TargetType, "|", mapping.TargetAssembly);
        }

        internal static FileStream? AcquireOutputLock(string outputPath, TimeSpan timeout)
        {
            // The lock file is intentionally left in place: deleting it on release would let two processes lock
            // different files with the same path. It's opened read-only, so a read-only lock file left behind by
            // another user or container still works. It's the one of the file a symbolic link points to, which is the one
            // written (see WriteAtomically), so writers through the link and through the file share it.
            var lockPath = ResolveMapPath(outputPath) + ".lock";
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.None);
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
                catch (IOException) when (stopwatch.Elapsed < timeout && File.Exists(lockPath))
                {
                    // Held by another process. If the lock file doesn't even exist it couldn't be created
                    // (read-only volume, invalid path...): waiting wouldn't help.
                    Thread.Sleep(10);
                }
                catch (IOException)
                {
                    return null;
                }
            }
        }

        internal static bool TryReadExistingMappings(string outputPath, out List<MapEntry> mappings)
        {
            mappings = [];
            string json;
            try
            {
                if (!File.Exists(outputPath))
                {
                    return true;
                }

                json = File.ReadAllText(outputPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }

            try
            {
                if (JsonHelper.DeserializeObject<MapDocument>(json) is { Mappings: { } existingMappings })
                {
                    mappings = existingMappings;
                }
            }
            catch
            {
                // A map that isn't valid JSON can't be merged: keep it next to the output for inspection rather than
                // silently replacing it.
                try
                {
                    File.Move(outputPath, $"{outputPath}.{Guid.NewGuid():N}.invalid");
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        internal static void WriteAtomically(string outputPath, string contents, Encoding? encoding = null)
        {
            outputPath = ResolveMapPath(outputPath);

            // A map that can't be written isn't replaced either (replacing it only needs to write its directory). Opening it is
            // the check: on Unix, the read-only attribute reflects the permission bits, which don't apply to root.
            if (File.Exists(outputPath))
            {
                using (new FileStream(outputPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                }
            }

            // Readers, and a process that writes without the lock, never see a partially written map.
            var temporaryOutputPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                if (encoding is null)
                {
                    File.WriteAllText(temporaryOutputPath, contents);
                }
                else
                {
                    File.WriteAllText(temporaryOutputPath, contents, encoding);
                }

#if NETCOREAPP3_0_OR_GREATER
                File.Move(temporaryOutputPath, outputPath, overwrite: true);
#else
                if (File.Exists(outputPath))
                {
                    // Without a backup name, a failed rename step of ReplaceFile leaves no map at all. With one, the
                    // previous map is either still in place or under the backup name, so it can always be restored.
                    var backupPath = $"{outputPath}.{Guid.NewGuid():N}.bak";
                    try
                    {
                        File.Replace(temporaryOutputPath, outputPath, backupPath);
                    }
                    finally
                    {
                        if (!File.Exists(outputPath) && File.Exists(backupPath))
                        {
                            File.Move(backupPath, outputPath);
                        }
                        else
                        {
                            File.Delete(backupPath);
                        }
                    }
                }
                else
                {
                    File.Move(temporaryOutputPath, outputPath);
                }
#endif
            }
            finally
            {
                try
                {
                    File.Delete(temporaryOutputPath);
                }
                catch
                {
                    // Best effort: the staging file normally no longer exists here.
                }
            }
        }

        /// <summary>
        /// Represents map document.
        /// </summary>
        internal sealed class MapDocument
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
        internal sealed class MapEntry
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

        /// <summary>
        /// Recording state, created on first use only: processes that don't record mappings don't pay for it.
        /// </summary>
        private static class RecorderState
        {
            /// <summary>
            /// Stores the recorded mappings.
            /// </summary>
            /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
            internal static readonly ConcurrentDictionary<string, MapEntry> Mappings = new(StringComparer.Ordinal);

            internal static readonly object FlushLock = new();
        }
    }
}
