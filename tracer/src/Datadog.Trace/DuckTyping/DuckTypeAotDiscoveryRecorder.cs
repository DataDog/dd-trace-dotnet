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
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

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
            if (proxyType is null || targetType is null)
            {
                return;
            }

            foreach (var mapEntry in GetMapEntries(proxyType, targetType, reverse))
            {
                EnsureProcessExitHook();
                if (!RecorderState.Mappings.TryAdd(GetKey(mapEntry), mapEntry))
                {
                    continue;
                }

                // Merging into the shared map rereads it, so don't do it on the thread that is creating a proxy.
                if ((Interlocked.Increment(ref _pendingMappings) % PeriodicFlushThreshold) == 0)
                {
                    ThreadPool.UnsafeQueueUserWorkItem(static _ => Flush(isExplicit: false), null);
                }
            }
        }

        /// <summary>
        /// Gets the map entries recorded for a mapping: its own (unless a registry can't name its types), and for a forward one,
        /// the mapping of the type the registry serves the target type with:
        /// <list type="bullet">
        /// <item>A proxy type dynamic duck typing generated: for a reverse proxy, the type it was created for (a class it derives
        /// from or an interface it implements); otherwise the base class of a generated class proxy.</item>
        /// <item>A class of the core library a registry can't name (it may not exist on another runtime, e.g. NativeAOT): the
        /// closest public class of the core library it derives from, whose proxy serves the classes other runtimes have instead
        /// (see DuckTypeAotEngine.TryGetFallbackResult).</item>
        /// </list>
        /// </summary>
        /// <param name="proxyType">The proxy type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="reverse">Whether the mapping is a reverse one.</param>
        /// <returns>The map entries.</returns>
        internal static List<MapEntry> GetMapEntries(Type proxyType, Type targetType, bool reverse)
        {
            var mapEntries = new List<MapEntry>(2);
            if (TryCreateMapEntry(proxyType, targetType, reverse, out var mapEntry))
            {
                mapEntries.Add(mapEntry);
            }

            if (reverse)
            {
                return mapEntries;
            }

            Type? servingType = null;
            if (targetType.Assembly.IsDynamic)
            {
                if (typeof(IDuckType).IsAssignableFrom(targetType))
                {
                    servingType = DuckType.GetDynamicReverseProxyDefinitionType(targetType) ??
                                  (targetType.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType) ? baseType : null);
                }
            }
            else if (DuckTypeAotEngine.IsRuntimeInternalType(targetType))
            {
                for (servingType = targetType.BaseType; servingType is not null && DuckTypeAotEngine.IsRuntimeInternalType(servingType); servingType = servingType.BaseType)
                {
                }

                if (servingType == typeof(object))
                {
                    servingType = null;
                }
            }

            if (servingType is not null && TryCreateMapEntry(proxyType, servingType, reverse: false, out var servingMapEntry))
            {
                mapEntries.Add(servingMapEntry);
            }

            return mapEntries;
        }

        /// <summary>
        /// Creates the map entry of a mapping. A type of a dynamic assembly (e.g. a proxy type dynamic duck typing generated)
        /// has none: a registry can't reference it, and the generation would fail.
        /// </summary>
        /// <param name="proxyType">The proxy type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="reverse">Whether the mapping is a reverse one.</param>
        /// <param name="mapEntry">The map entry.</param>
        /// <returns>true if the mapping has a map entry; otherwise, false.</returns>
        internal static bool TryCreateMapEntry(Type proxyType, Type targetType, bool reverse, out MapEntry mapEntry)
        {
            mapEntry = null!;
            if (proxyType.Assembly.IsDynamic || targetType.Assembly.IsDynamic)
            {
                return false;
            }

            var proxyTypeName = proxyType.FullName;
            var targetTypeName = targetType.FullName;
            var proxyAssembly = proxyType.Assembly.GetName().Name;
            var targetAssembly = targetType.Assembly.GetName().Name;
            if (StringUtil.IsNullOrWhiteSpace(proxyTypeName) ||
                StringUtil.IsNullOrWhiteSpace(targetTypeName) ||
                StringUtil.IsNullOrWhiteSpace(proxyAssembly) ||
                StringUtil.IsNullOrWhiteSpace(targetAssembly))
            {
                return false;
            }

            mapEntry = new MapEntry
            {
                Mode = reverse ? "reverse" : "forward",
                ProxyType = proxyTypeName,
                ProxyAssembly = proxyAssembly,
                TargetType = targetTypeName,
                TargetAssembly = targetAssembly
            };
            return true;
        }

        /// <summary>
        /// Ensures ensure process exit hook.
        /// </summary>
        private static void EnsureProcessExitHook()
        {
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
                // Nothing was recorded since the last flush (e.g. the exit flush after the test framework one).
                if (pendingMappings == 0)
                {
                    return;
                }

                var directory = Path.GetDirectoryName(outputPath);
                if (!StringUtil.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Child processes (e.g. one testhost per target framework) inherit the output path. A lock file
                // serializes the read-merge-write of every process so none of them drops the others' mappings.
                using var outputLock = AcquireOutputLock(outputPath, isExplicit ? ExplicitFlushLockTimeout : TimeSpan.Zero);
                // A periodic flush doesn't wait for the lock. An explicit flush only gets here without the lock
                // when it can't be acquired at all (timeout, or a lock file this process can't open) and writes anyway.
                if (outputLock is null && !isExplicit)
                {
                    return;
                }

                // Never overwrite a map that couldn't be read; a later flush retries.
                if (!TryMergeIntoMap(outputPath, RecorderState.Mappings.Values))
                {
                    return;
                }

                // A write without the lock may still be overwritten by a process that read the map before it, so
                // keep the mappings pending and let the next flush (e.g. the exit one) write them again.
                if (outputLock is not null)
                {
                    _ = Interlocked.Add(ref _pendingMappings, -pendingMappings);
                }
            }
            catch
            {
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

        /// <summary>
        /// Adds recorded mappings to a map: the mappings it doesn't have yet are added (sorted) after its own, and its other
        /// properties are kept (not its comments).
        /// </summary>
        /// <param name="outputPath">The map path.</param>
        /// <param name="recordedMappings">The recorded mappings.</param>
        /// <returns>true if the map was written; false if it couldn't be read (it isn't replaced).</returns>
        internal static bool TryMergeIntoMap(string outputPath, IEnumerable<MapEntry> recordedMappings)
        {
            if (!TryReadExistingMap(outputPath, out var document, out var existingMappings))
            {
                return false;
            }

            document ??= new JObject();
            if (document["mappings"] is not JArray mappingsArray)
            {
                mappingsArray = new JArray();
                document["mappings"] = mappingsArray;
            }

            var keys = new HashSet<string>(existingMappings.Select(GetKey), StringComparer.Ordinal);
            foreach (var mapping in recordedMappings
                                   .Where(mapping => keys.Add(GetKey(mapping)))
                                   .OrderBy(mapping => mapping.Mode, StringComparer.Ordinal)
                                   .ThenBy(mapping => mapping.ProxyAssembly, StringComparer.Ordinal)
                                   .ThenBy(mapping => mapping.ProxyType, StringComparer.Ordinal)
                                   .ThenBy(mapping => mapping.TargetAssembly, StringComparer.Ordinal)
                                   .ThenBy(mapping => mapping.TargetType, StringComparer.Ordinal))
            {
                mappingsArray.Add(new JObject
                {
                    ["mode"] = mapping.Mode,
                    ["proxyType"] = mapping.ProxyType,
                    ["proxyAssembly"] = mapping.ProxyAssembly,
                    ["targetType"] = mapping.TargetType,
                    ["targetAssembly"] = mapping.TargetAssembly,
                });
            }

            WriteAtomically(outputPath, JsonHelper.TokenToString(document, Formatting.Indented));
            return true;
        }

        /// <summary>
        /// Gets the path of the lock file of a map: the one of the file a symbolic link points to, which is the one written (see
        /// WriteAtomically), so writers through the link and through the file share it.
        /// </summary>
        /// <param name="outputPath">The map path.</param>
        /// <returns>The lock file path.</returns>
        internal static string GetOutputLockPath(string outputPath) => ResolveMapPath(outputPath) + ".lock";

        internal static FileStream? AcquireOutputLock(string outputPath, TimeSpan timeout)
        {
            // The lock file is intentionally left in place: deleting it on release would let two processes lock
            // different files with the same path. It's opened read-only, so a read-only lock file left behind by
            // another user or container still works.
            var lockPath = GetOutputLockPath(outputPath);
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
            => TryReadExistingMap(outputPath, out _, out mappings);

        /// <summary>
        /// Reads the map of other processes (or of a user). A missing map (or one a dangling symbolic link points to) is empty; a
        /// map that isn't JSON is set aside; a map that isn't UTF-8 without a byte order mark telling its encoding (e.g. UTF-16
        /// without one) can't be read, and isn't replaced.
        /// </summary>
        /// <param name="outputPath">The map path.</param>
        /// <param name="document">The map document, or null when there is none.</param>
        /// <param name="mappings">The mappings of the map.</param>
        /// <returns>true if the map can be replaced; otherwise, false.</returns>
        private static bool TryReadExistingMap(string outputPath, out JObject? document, out List<MapEntry> mappings)
        {
            document = null;
            mappings = [];
            string json;
            try
            {
                var bytes = File.ReadAllBytes(outputPath);
                using var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
                json = reader.ReadToEnd();
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                return false;
            }

            // ASCII text in UTF-16 or UTF-32 without a byte order mark is valid UTF-8 with NUL characters.
            if (json.IndexOf('\0') >= 0)
            {
                return false;
            }

            if (StringUtil.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            try
            {
                document = LoadDocument(json);
                if (document["mappings"] is { } mappingsToken)
                {
                    foreach (var entry in (JArray)mappingsToken)
                    {
                        var mapping = (JObject)entry;
                        mappings.Add(new MapEntry
                        {
                            Mode = (string?)mapping["mode"] ?? string.Empty,
                            ProxyType = (string?)mapping["proxyType"] ?? string.Empty,
                            ProxyAssembly = (string?)mapping["proxyAssembly"],
                            TargetType = (string?)mapping["targetType"] ?? string.Empty,
                            TargetAssembly = (string?)mapping["targetAssembly"],
                        });
                    }
                }
            }
            catch
            {
                // A map that isn't a JSON object with a 'mappings' array of objects can't be merged: keep it next to the output
                // for inspection rather than silently replacing it.
                document = null;
                mappings = [];
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

            static JObject LoadDocument(string json)
            {
                // Dates and decimal numbers are written back as written; numbers beyond the range of decimal as doubles.
                try
                {
                    return LoadDocumentWith(json, FloatParseHandling.Decimal);
                }
                catch (JsonReaderException)
                {
                    return LoadDocumentWith(json, FloatParseHandling.Double);
                }
            }

            static JObject LoadDocumentWith(string json, FloatParseHandling floatParseHandling)
            {
                using var reader = new JsonTextReader(new StringReader(json)) { ArrayPool = JsonArrayPool.Shared, DateParseHandling = DateParseHandling.None, FloatParseHandling = floatParseHandling };
                return JObject.Load(reader);
            }
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
            /// Gets or sets proxy assembly (none for an assembly-qualified proxy type name).
            /// </summary>
            /// <value>The proxy assembly value.</value>
            [JsonProperty("proxyAssembly")]
            public string? ProxyAssembly { get; set; }

            /// <summary>
            /// Gets or sets target type.
            /// </summary>
            /// <value>The target type value.</value>
            [JsonProperty("targetType")]
            public string TargetType { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets target assembly (none for an assembly-qualified target type name).
            /// </summary>
            /// <value>The target assembly value.</value>
            [JsonProperty("targetAssembly")]
            public string? TargetAssembly { get; set; }
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
