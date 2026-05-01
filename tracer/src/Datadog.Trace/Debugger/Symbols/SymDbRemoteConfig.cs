// <copyright file="SymDbRemoteConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Debugger.Symbols
{
    internal sealed class SymDbRemoteConfig : IDisposable
    {
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
            if (TryGetUploadSymbols(addedConfig, out var uploadSymbols))
            {
                _onUploadSymbolsChanged(uploadSymbols);
            }

            return [];
        }

        internal static bool TryGetUploadSymbols(Dictionary<string, List<RemoteConfiguration>> addedConfig, out bool uploadSymbols)
        {
            uploadSymbols = false;

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
                if (rawFile.Deserialize<SymDbEnablement>().TypedFile is { } enablement)
                {
                    uploadSymbols = enablement.UploadSymbols;
                    return true;
                }
            }

            return false;
        }
    }
}
