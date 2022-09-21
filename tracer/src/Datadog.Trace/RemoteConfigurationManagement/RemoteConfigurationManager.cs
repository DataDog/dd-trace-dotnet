// <copyright file="RemoteConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Datadog.Trace.Util;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class RemoteConfigurationManager : IRemoteConfigurationManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RemoteConfigurationManager));
        private static readonly object LockObject = new object();
        private static readonly ConcurrentQueue<Action<RemoteConfigurationManager>> _initializationQueue = new ConcurrentQueue<Action<RemoteConfigurationManager>>();

        private readonly string _id;
        private readonly RcmClientTracer _rcmTracer;
        private readonly IDiscoveryService _discoveryService;
        private readonly IRemoteConfigurationApi _remoteConfigurationApi;
        private readonly TimeSpan _pollInterval;

        private readonly CancellationTokenSource _cancellationSource;
        private readonly ConcurrentDictionary<string, Product> _products;

        // 32 capabilities ought to be enough for anybody
        private BitVector32 _capabilities = new();

        private int _rootVersion;
        private int _targetsVersion;
        private string? _lastPollError;
        private bool _isPollingStarted;
        private bool _isRcmEnabled;

        private RemoteConfigurationManager(
            IDiscoveryService discoveryService,
            IRemoteConfigurationApi remoteConfigurationApi,
            string id,
            RcmClientTracer rcmTracer,
            TimeSpan pollInterval)
        {
            _discoveryService = discoveryService;
            _remoteConfigurationApi = remoteConfigurationApi;
            _rcmTracer = rcmTracer;
            _pollInterval = pollInterval;
            _id = id;

            _rootVersion = 1;
            _targetsVersion = 0;
            _lastPollError = null;
            _cancellationSource = new CancellationTokenSource();
            _products = new ConcurrentDictionary<string, Product>();
            discoveryService.SubscribeToChanges(SetRcmEnabled);
        }

        public static RemoteConfigurationManager? Instance { get; private set; }

        public static RemoteConfigurationManager Create(
            IDiscoveryService discoveryService,
            IRemoteConfigurationApi remoteConfigurationApi,
            RemoteConfigurationSettings settings,
            string serviceName,
            string? environment,
            string? serviceVersion)
        {
            lock (LockObject)
            {
                Instance ??= new RemoteConfigurationManager(
                    discoveryService,
                    remoteConfigurationApi,
                    id: settings.Id,
                    rcmTracer: new RcmClientTracer(settings.RuntimeId, settings.TracerVersion, serviceName, environment, serviceVersion),
                    pollInterval: settings.PollInterval);
            }

            while (_initializationQueue.TryDequeue(out var action))
            {
                action(Instance);
            }

            return Instance;
        }

        public static void CallbackWithInitializedInstance(Action<RemoteConfigurationManager> action)
        {
            RemoteConfigurationManager? inst = null;
            lock (LockObject)
            {
                inst = Instance;
                if (inst == null)
                {
                    _initializationQueue.Enqueue(action);
                    return;
                }
            }

            action(inst);
        }

        public async Task StartPollingAsync()
        {
            lock (LockObject)
            {
                if (_isPollingStarted)
                {
                    Log.Warning("Remote Configuration management polling is already started.");
                    return;
                }

                _isPollingStarted = true;
                LifetimeManager.Instance.AddShutdownTask(OnShutdown);
            }

            while (!_cancellationSource.IsCancellationRequested)
            {
                var isRcmEnabled = Volatile.Read(ref _isRcmEnabled);
                var isProductRegistered = _products.Any();

                if (isRcmEnabled && isProductRegistered)
                {
                    await Poll().ConfigureAwait(false);
                    _lastPollError = null;
                }

                try
                {
                    await Task.Delay(_pollInterval, _cancellationSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // We are shutting down, so don't do anything about it
                }
            }
        }

        public void RegisterProduct(Product product)
        {
            _products.TryAdd(product.Name, product);
        }

        public void UnregisterProduct(string productName)
        {
            _products.TryRemove(productName, out _);
        }

        public void OnShutdown()
        {
            _discoveryService.RemoveSubscription(SetRcmEnabled);
            _cancellationSource.Cancel();
        }

        public void SetCapability(int index, bool available)
        {
            _capabilities[index] = available;
        }

        private async Task Poll()
        {
            try
            {
                var products = _products.ToDictionary(pair => pair.Key, pair => pair.Value);

                var request = BuildRequest(products);
                var response = await _remoteConfigurationApi.GetConfigs(request).ConfigureAwait(false);

                if (response?.Targets?.Signed != null)
                {
                    ProcessResponse(response, products);
                    _targetsVersion = response.Targets.Signed.Version;
                }
            }
            catch (Exception e)
            {
                _lastPollError = e.Message;
            }
        }

        private GetRcmRequest BuildRequest(IDictionary<string, Product> products)
        {
            var appliedConfigurations = products.Values.SelectMany(pair => pair.AppliedConfigurations.Values);

            var cachedTargetFiles = new List<RcmCachedTargetFile>();
            var configStates = new List<RcmConfigState>();

            foreach (var cache in appliedConfigurations)
            {
                cachedTargetFiles.Add(new RcmCachedTargetFile(cache.Path.Path, cache.Length, cache.Hashes.Select(kp => new RcmCachedTargetFileHash(kp.Key, kp.Value)).ToList()));
                configStates.Add(new RcmConfigState(cache.Path.Id, cache.Version, cache.Path.Product, cache.ApplyState, cache.Error));
            }

            var rcmState = new RcmClientState(_rootVersion, _targetsVersion, configStates, _lastPollError != null, _lastPollError);
            var rcmClient = new RcmClient(_id, products.Keys, _rcmTracer, rcmState, BitConverter.GetBytes(_capabilities.Data));
            var rcmRequest = new GetRcmRequest(rcmClient, cachedTargetFiles);

            return rcmRequest;
        }

        private void ProcessResponse(GetRcmResponse response, IDictionary<string, Product> products)
        {
            var actualConfigPath =
                response
                   .ClientConfigs
                   .Select(RemoteConfigurationPath.FromPath)
                   .ToDictionary(path => path.Path);

            var changedConfigurationsByProduct = GetChangedConfigurations()
               .GroupBy(config => config.Path.Product);

            // copy products
            foreach (var productGroup in changedConfigurationsByProduct)
            {
                var product = products[productGroup.Key];

                try
                {
                    var configurations = productGroup.ToList();

                    product.AssignConfigs(configurations);
                }
                catch (Exception e)
                {
                    Log.Warning($"Failed to apply remote configurations {e.Message}");
                }
            }

            UnapplyRemovedConfigurations();

            IEnumerable<RemoteConfiguration> GetChangedConfigurations()
            {
                var signed = response.Targets.Signed.Targets;
                var targetFiles = (response.TargetFiles ?? Enumerable.Empty<RcmFile>()).ToDictionary(f => f.Path, f => f);

                foreach (var kp in actualConfigPath)
                {
                    if (!signed.TryGetValue(kp.Key, out var signedTarget))
                    {
                        ThrowHelper.ThrowException($"Missing config {kp.Key} in targets");
                    }

                    if (!products.TryGetValue(kp.Value.Product, out var product))
                    {
                        ThrowHelper.ThrowException($"Received config {kp.Key} for a product that was not requested");
                    }

                    var isConfigApplied = product.AppliedConfigurations.TryGetValue(kp.Key, out var appliedConfig) && appliedConfig.Hashes.SequenceEqual(signedTarget.Hashes);
                    if (isConfigApplied)
                    {
                        continue;
                    }

                    yield return new RemoteConfiguration(kp.Value, targetFiles[kp.Key].Raw, signedTarget.Length, signedTarget.Hashes, signedTarget.Custom.V);
                }
            }

            void UnapplyRemovedConfigurations()
            {
                List<string>? remove = null;

                foreach (var product in products.Values)
                {
                    foreach (var appliedConfiguration in product.AppliedConfigurations)
                    {
                        if (!actualConfigPath.ContainsKey(appliedConfiguration.Key))
                        {
                            remove ??= new List<string>();
                            remove.Add(appliedConfiguration.Key);
                        }
                    }

                    if (remove is not null)
                    {
                        foreach (var key in remove)
                        {
                            product.AppliedConfigurations.Remove(key);
                        }

                        remove.Clear();
                    }
                }
            }
        }

        private void SetRcmEnabled(AgentConfiguration c)
        {
            _isRcmEnabled = !string.IsNullOrEmpty(c.ConfigurationEndpoint);
        }
    }
}
