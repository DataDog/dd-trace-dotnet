// <copyright file="SymDbRemoteConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Debugger.Symbols
{
    internal sealed class SymDbRemoteConfig : IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SymDbRemoteConfig>();

        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly Action<bool> _onUploadSymbolsChanged;
        private readonly object _lock = new();
        private ISubscription? _subscription;
        private bool _disposed;

        public SymDbRemoteConfig(IRcmSubscriptionManager subscriptionManager, Action<bool> onUploadSymbolsChanged)
        {
            _subscriptionManager = subscriptionManager;
            _onUploadSymbolsChanged = onUploadSymbolsChanged;
        }

        public void Subscribe()
        {
            lock (_lock)
            {
                if (_disposed || _subscription is not null)
                {
                    return;
                }

                _subscription = new Subscription(OnRemoteConfigurationChanged, RcmProducts.LiveDebuggingSymbolDb);
                _subscriptionManager.SubscribeToChanges(_subscription);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                if (_subscription is { } subscription)
                {
                    _subscriptionManager.Unsubscribe(subscription);
                }

                _subscription = null;
            }
        }

        private ApplyDetails[] OnRemoteConfigurationChanged(Dictionary<string, List<RemoteConfiguration>> addedConfig, Dictionary<string, List<RemoteConfigurationPath>>? removedConfig)
        {
            if (TryGetUploadSymbols(addedConfig, out var uploadSymbols, out var configPath, out var deserializationError))
            {
                _onUploadSymbolsChanged(uploadSymbols);
                return [ApplyDetails.FromOk(configPath)];
            }

            if (deserializationError is not null)
            {
                return [ApplyDetails.FromError(configPath, deserializationError)];
            }

            // Treat removal of the SymDB enablement payload as upload_symbols=false
            if (removedConfig is not null
                && removedConfig.TryGetValue(RcmProducts.LiveDebuggingSymbolDb, out var removedPaths))
            {
                foreach (var path in removedPaths)
                {
                    if (path.Id.StartsWith(DefinitionPaths.SymDB, StringComparison.Ordinal))
                    {
                        _onUploadSymbolsChanged(false);
                        break;
                    }
                }
            }

            return [];
        }

        internal static bool TryGetUploadSymbols(Dictionary<string, List<RemoteConfiguration>> addedConfig, out bool uploadSymbols)
            => TryGetUploadSymbols(addedConfig, out uploadSymbols, out _, out _);

        internal static bool TryGetUploadSymbols(Dictionary<string, List<RemoteConfiguration>> addedConfig, out bool uploadSymbols, out string configPath)
            => TryGetUploadSymbols(addedConfig, out uploadSymbols, out configPath, out _);

        internal static bool TryGetUploadSymbols(Dictionary<string, List<RemoteConfiguration>> addedConfig, out bool uploadSymbols, out string configPath, out string? deserializationError)
        {
            uploadSymbols = false;
            configPath = string.Empty;
            deserializationError = null;

            if (!addedConfig.TryGetValue(RcmProducts.LiveDebuggingSymbolDb, out var configs))
            {
                return false;
            }

            foreach (var remoteConfiguration in configs)
            {
                if (!remoteConfiguration.Path.Id.StartsWith(DefinitionPaths.SymDB, StringComparison.Ordinal))
                {
                    continue;
                }

                var rawFile = new NamedRawFile(remoteConfiguration.Path, remoteConfiguration.Contents);
                try
                {
                    if (rawFile.Deserialize<SymDbEnablement>().TypedFile is { } enablement)
                    {
                        uploadSymbols = enablement.UploadSymbols;
                        configPath = remoteConfiguration.Path.Path;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to deserialize SymDB enablement payload {Path}", remoteConfiguration.Path.Path);
                    configPath = remoteConfiguration.Path.Path;
                    deserializationError = ex.Message;
                    return false;
                }
            }

            return false;
        }
    }
}
