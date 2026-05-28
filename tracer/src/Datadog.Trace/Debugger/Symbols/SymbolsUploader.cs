// <copyright file="SymbolsUploader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Symbols.Model;
using Datadog.Trace.Debugger.ThirdParty;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using OperationCanceledException = System.OperationCanceledException;

namespace Datadog.Trace.Debugger.Symbols
{
    internal sealed class SymbolsUploader : IDebuggerUploader
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SymbolsUploader));

        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly Lazy<string> _serviceName;
        private readonly string? _serviceVersion;
        private readonly string? _environment;
        private readonly SemaphoreSlim _assemblySemaphore;
        private readonly SemaphoreSlim _discoveryServiceSemaphore;
        private readonly HashSet<string> _alreadyProcessed;
        private readonly ImmutableHashSet<string> _symDb3rdPartyIncludes;
        private readonly ImmutableHashSet<string> _symDb3rdPartyExcludes;
        private readonly long _thresholdInBytes;
        private readonly TaskCompletionSource<bool> _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ISymbolUploadApi _api;
        private readonly object _disposeLock = new();
        private readonly IDiscoveryService _discoveryService;

        // The (uploadId, batchNum) pair groups all batches uploaded by this
        // uploader instance. The same values are stamped into both the
        // attachment Root and the EvP event metadata so the backend can
        // match them up.
        private readonly Guid _symdbUploadId = Guid.NewGuid();
        private volatile bool _disposed;
        private byte[]? _payload;
        private volatile string? _symDbEndpoint;
        private long _symdbBatchNum;

        private SymbolsUploader(
            ISymbolUploadApi api,
            IDiscoveryService discoveryService,
            DebuggerSettings settings,
            TracerSettings tracerSettings,
            Func<string> serviceNameProvider)
        {
            _symDbEndpoint = null;
            _alreadyProcessed = new HashSet<string>();
            _environment = tracerSettings.Manager.InitialMutableSettings.Environment;
            _serviceVersion = tracerSettings.Manager.InitialMutableSettings.ServiceVersion;
            // Capture service name on first use and keep it fixed for the lifetime of this uploader instance
            _serviceName = new Lazy<string>(serviceNameProvider, LazyThreadSafetyMode.ExecutionAndPublication);
            _discoveryService = discoveryService;
            _api = api;
            _assemblySemaphore = new SemaphoreSlim(1);
            _discoveryServiceSemaphore = new SemaphoreSlim(0);
            _thresholdInBytes = settings.SymbolDatabaseBatchSizeInBytes;
            _symDb3rdPartyIncludes = settings.SymDbThirdPartyDetectionIncludes;
            _symDb3rdPartyExcludes = settings.SymDbThirdPartyDetectionExcludes;
            _jsonSerializerSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            _discoveryService.SubscribeToChanges(ConfigurationChanged);
        }

        private void ConfigurationChanged(AgentConfiguration configuration)
        {
            if (_disposed || _processExit.Task.IsCompleted)
            {
                return;
            }

            if (string.IsNullOrEmpty(configuration.SymbolDbEndpoint))
            {
                Log.Debug("`SymbolDb endpoint` is null. This can happen if your datadog-agent version is lower than 7.45");
                return;
            }

            _symDbEndpoint = configuration.SymbolDbEndpoint;
            _discoveryService.RemoveSubscription(ConfigurationChanged);
            try
            {
                _discoveryServiceSemaphore.Release(1);
            }
            catch (ObjectDisposedException)
            {
                // The uploader may be disabled while discovery publishes an update.
            }
        }

        public static IDebuggerUploader Create(ISymbolUploadApi api, IDiscoveryService discoveryService, TracerSettings tracerSettings, DebuggerSettings settings, Func<string> serviceNameProvider)
        {
            if (!settings.SymbolDatabaseUploadEnabled)
            {
                Log.Information("Symbol database uploading is disabled. To enable it, please set {EnvironmentVariable} environment variable to 'true'.", ConfigurationKeys.Debugger.SymbolDatabaseUploadEnabled);
                return NoOpSymbolUploader.Instance;
            }

            // TODO: we need to be able to update the tracer settings dynamically
            return new SymbolsUploader(api, discoveryService, settings, tracerSettings, serviceNameProvider);
        }

        private void RegisterToAssemblyLoadEvent()
        {
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
        }

        private void UnRegisterToAssemblyLoadEvent()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= CurrentDomain_AssemblyLoad;
        }

        private void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs args)
        {
            _ = ProcessItemAsync(args.LoadedAssembly);
        }

        private async Task ProcessItemAsync(Assembly assembly)
        {
            if (_disposed)
            {
                return;
            }

            await Task.Yield();

            bool semaphoreAcquired = false;
            try
            {
                var acquireTimeout = TimeSpan.FromSeconds(5);
                while (!_processExit.Task.IsCompleted && !semaphoreAcquired)
                {
                    semaphoreAcquired = await _assemblySemaphore.WaitAsync(acquireTimeout).ConfigureAwait(false);
                }

                if (_processExit.Task.IsCompleted || _disposed)
                {
                    return;
                }

                await ProcessItem(assembly).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
            }
            catch (ObjectDisposedException)
            {
                // Handle disposal gracefully
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing assembly {Assembly}", assembly);
            }
            finally
            {
                if (semaphoreAcquired)
                {
                    try
                    {
                        _assemblySemaphore.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Semaphore already disposed, ignore
                    }
                }
            }
        }

        private async Task ProcessItem(Assembly assembly)
        {
            // Disable/dispose is cooperative: already-running extraction or upload can finish one bounded batch.
            try
            {
                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName))
                {
                    return;
                }

                if (AssemblyFilter.ShouldSkipAssembly(assembly, _symDb3rdPartyExcludes, _symDb3rdPartyIncludes))
                {
                    return;
                }

                if (!_alreadyProcessed.Add(assembly.Location))
                {
                    return;
                }

                await UploadAssemblySymbols(assembly).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while trying to extract assembly symbol {Assembly}", assembly);
            }
        }

        private async Task UploadAssemblySymbols(Assembly assembly)
        {
            using var symbolsExtractor = SymbolExtractor.Create(assembly);
            if (symbolsExtractor == null)
            {
                return;
            }

            if (!symbolsExtractor.TryGetAssemblySymbol(out var assemblyScope))
            {
                return;
            }

            var root = new Root
            {
                Service = _serviceName.Value,
                Env = _environment,
                Language = "dotnet",
                Version = _serviceVersion,
                Scopes = [assemblyScope]
            };

            await UploadClasses(root, symbolsExtractor.GetClassSymbols()).ConfigureAwait(false);
        }

        private async Task UploadClasses(Root root, IEnumerable<Model.Scope> classes)
        {
            // The payload builder is used for the whole upload: each batch
            // starts by appending a fresh prefix (with a per-batch batch_num),
            // class scopes are appended directly into it, and on flush we
            // append ']' + suffix and send.
            var payloadBuilder = StringBuilderCache.Acquire((int)_thresholdInBytes + 256);

            var serializer = JsonSerializer.Create(_jsonSerializerSettings);
            using var pooledWriter = new Utf8CountingPooledTextWriter();

            try
            {
                if (!TryStartBatch(root, payloadBuilder, out var suffix, out var batchNum))
                {
                    return;
                }

                var accumulatedBytes = 0;
                var hasAnyClass = false;

                foreach (var classSymbol in classes)
                {
                    if (classSymbol == default)
                    {
                        continue;
                    }

                    // Try to serialize and append the class
                    if (!TrySerializeClass(classSymbol, payloadBuilder, hasAnyClass, serializer, pooledWriter, accumulatedBytes, out var newByteCount))
                    {
                        // If we couldn't append because it would exceed capacity,
                        // upload current batch first
                        bool succeeded = false;
                        if (hasAnyClass)
                        {
                            await Flush(payloadBuilder, suffix, batchNum).ConfigureAwait(false);
                            payloadBuilder.Length = 0;
                            accumulatedBytes = 0;
                            hasAnyClass = false;
                            if (!TryStartBatch(root, payloadBuilder, out suffix, out batchNum))
                            {
                                return;
                            }

                            // Try again with empty scopes in the new batch
                            succeeded = TrySerializeClass(classSymbol, payloadBuilder, hasAnyClass, serializer, pooledWriter, accumulatedBytes, out newByteCount);
                        }

                        if (!succeeded)
                        {
                            // If it still doesn't fit, this single class is too large
                            Log.Warning("Class {Name} exceeds maximum capacity", classSymbol.Name);
                            continue;
                        }
                    }

                    accumulatedBytes = newByteCount;
                    hasAnyClass = true;
                }

                // Upload any remaining data
                if (hasAnyClass)
                {
                    await Flush(payloadBuilder, suffix, batchNum).ConfigureAwait(false);
                }
            }
            finally
            {
                StringBuilderCache.Release(payloadBuilder);
            }
        }

        private bool TryStartBatch(Root root, StringBuilder builder, out string suffix, out long batchNum)
        {
            batchNum = Interlocked.Increment(ref _symdbBatchNum);
            root.UploadId = _symdbUploadId;
            root.BatchNum = batchNum;
            root.Final = false;
            var rootAsString = JsonHelper.SerializeObject(root);
            if (!TryBuildPrefixAndSuffix(rootAsString, out var prefix, out suffix))
            {
                // this should not happen unless Root/Scope JSON shape changes
                Log.Warning("Unable to find insertion point for class scopes in SymDB payload");
                return false;
            }

            builder.Append(prefix);
            return true;
        }

        private async Task Flush(StringBuilder builder, string suffix, long batchNum)
        {
            builder.Append(']');
            builder.Append(suffix);
            var metadata = new SymDbUploadMetadata(
                Service: _serviceName.Value,
                Version: _serviceVersion,
                UploadId: _symdbUploadId,
                BatchNum: batchNum,
                // Always false: the .NET tracer continuously uploads new code as
                // assemblies get loaded; there is no defined end-of-upload point.
                Final: false);
            await SendSymbol(builder.ToString(), metadata).ConfigureAwait(false);
        }

        private async Task<bool> SendSymbol(string symbol, SymDbUploadMetadata metadata)
        {
            // TODO: Consider streaming SymDB payload bytes directly to the stream
            // to avoid the extra `string -> UTF8 bytes` encoding step and intermediate byte[] allocations/copies.
            // Similar allocation reductions were done in https://github.com/DataDog/dd-trace-dotnet/pull/8017
            // and https://github.com/DataDog/dd-trace-dotnet/pull/8019).
            var count = Encoding.UTF8.GetByteCount(symbol);
            if (_payload == null || count > _payload.Length)
            {
                _payload = new byte[count];
            }

            Encoding.UTF8.GetBytes(symbol, 0, symbol.Length, _payload, 0);
            try
            {
                return await _api
                    .SendBatchAsync(new ArraySegment<byte>(_payload, 0, count), metadata)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.ErrorSkipTelemetry(e, "Error uploading symbol database");
                return false;
            }
        }

        private bool TrySerializeClass(
            Model.Scope classScope,
            StringBuilder payloadBuilder,
            bool hasAnyClass,
            JsonSerializer serializer,
            Utf8CountingPooledTextWriter pooledWriter,
            int currentBytes,
            out int newTotalBytes)
        {
            pooledWriter.Reset();
            using (var jsonWriter = new JsonTextWriter(pooledWriter) { CloseOutput = false, ArrayPool = JsonArrayPool.Shared })
            {
                serializer.Serialize(jsonWriter, classScope);
                jsonWriter.Flush();
            }

            var classBytes = pooledWriter.Utf8ByteCount + (hasAnyClass ? 1 : 0); // comma
            newTotalBytes = currentBytes + classBytes;

            if (newTotalBytes > _thresholdInBytes)
            {
                return false;
            }

            // Safe to append
            if (hasAnyClass)
            {
                payloadBuilder.Append(',');
            }

            payloadBuilder.Append(pooledWriter.Buffer, 0, pooledWriter.Length);
            return true;
        }

        private bool TryBuildPrefixAndSuffix(string rootAsString, out string prefix, out string suffix)
        {
            const string scopesNull = "\"scopes\":null";

            var index = rootAsString.IndexOf(scopesNull, StringComparison.Ordinal);
            if (index < 0)
            {
                prefix = string.Empty;
                suffix = string.Empty;
                return false;
            }

            // Insert '[' in place of 'null' (matching previous logic)
            var beforeNullEnd = index + scopesNull.Length - "null".Length;
#if NETCOREAPP
            prefix = string.Concat(rootAsString.AsSpan(0, beforeNullEnd), "[");
#else
            prefix = rootAsString.Substring(0, beforeNullEnd) + "[";
#endif
            suffix = rootAsString.Substring(index + scopesNull.Length);
            return true;
        }

        public async Task StartFlushingAsync()
        {
            if (await WaitForDiscoveryServiceAsync().ConfigureAwait(false) == false)
            {
                Log.Information("Dynamic Instrumentation won't upload symbols for auto-complete suggestions. This can happen if your datadog-agent version is lower than 7.45");
                return;
            }

            lock (_disposeLock)
            {
                if (_disposed || _processExit.Task.IsCompleted)
                {
                    return;
                }

                RegisterToAssemblyLoadEvent();
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                await ProcessItemAsync(assembly).ConfigureAwait(false);
            }
        }

        private async Task<bool> WaitForDiscoveryServiceAsync()
        {
            if (_disposed || _processExit.Task.IsCompleted)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(_symDbEndpoint))
            {
                // if it is already set, return immediately.
                // theoretically, this can be reverted in case of version downgrade, but we not support that atm.
                return true;
            }

            await Task.Yield();

            try
            {
                var completedTask = await Task.WhenAny(
                                                   _discoveryServiceSemaphore.WaitAsync(),
                                                   _processExit.Task)
                                              .ConfigureAwait(false);

                if (completedTask == _processExit.Task || _disposed)
                {
                    return false;
                }

                return !string.IsNullOrEmpty(_symDbEndpoint);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _processExit.TrySetResult(true);
                UnRegisterToAssemblyLoadEvent();
                _discoveryService.RemoveSubscription(ConfigurationChanged);
                _assemblySemaphore.Dispose();
                _discoveryServiceSemaphore.Dispose();
            }
        }
    }
}
