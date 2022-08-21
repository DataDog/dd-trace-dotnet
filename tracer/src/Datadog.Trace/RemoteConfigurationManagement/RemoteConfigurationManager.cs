// <copyright file="RemoteConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private static readonly SemaphoreSlim SemaphoreSlim = new(1, 1);

        private readonly string _id;
        private readonly RcmClientTracer _rcmTracer;
        private readonly IDiscoveryService _discoveryService;
        private readonly IRemoteConfigurationApi _remoteConfigurationApi;
        private readonly TimeSpan _pollInterval;

        private readonly CancellationTokenSource _cancellationSource;
        private readonly ConcurrentDictionary<string, Product> _products;

        private int _rootVersion;
        private int _targetsVersion;
        private string _lastPollError;
        private bool _isPollingStarted;

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
        }

        public static RemoteConfigurationManager Instance { get; private set; }

        public static RemoteConfigurationManager Create(
            IDiscoveryService discoveryService,
            IRemoteConfigurationApi remoteConfigurationApi,
            RemoteConfigurationSettings settings,
            string serviceName)
        {
            SemaphoreSlim.Wait();

            try
            {
                Instance ??= new RemoteConfigurationManager(
                    discoveryService,
                    remoteConfigurationApi,
                    id: settings.Id,
                    rcmTracer: new RcmClientTracer(settings.RuntimeId, settings.TracerVersion, serviceName, settings.Environment, settings.AppVersion),
                    pollInterval: settings.PollInterval);
            }
            finally
            {
                SemaphoreSlim.Release();
            }

            return Instance;
        }

        public async Task StartPollingAsync()
        {
            await SemaphoreSlim.WaitAsync().ConfigureAwait(false);

            if (_isPollingStarted)
            {
                Log.Warning("Remote Configuration management polling is already started.");
                return;
            }

            _isPollingStarted = true;
            LifetimeManager.Instance.AddShutdownTask(OnShutdown);

            SemaphoreSlim.Release();

            while (!_cancellationSource.IsCancellationRequested)
            {
                var isRcmEnabled = !string.IsNullOrEmpty(_discoveryService.ConfigurationEndpoint);
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
                catch (TaskCanceledException)
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

        private async Task Poll()
        {
            await SemaphoreSlim.WaitAsync().ConfigureAwait(false);

            try
            {
                var request = BuildRequest();
                var response = await _remoteConfigurationApi.GetConfigs(request).ConfigureAwait(false);

                if (response?.Targets?.Signed != null)
                {
                    ProcessResponse(response);
                    _targetsVersion = response.Targets.Signed.Version;
                }
            }
            catch (Exception e)
            {
                _lastPollError = e.Message;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        private GetRcmRequest BuildRequest()
        {
            var appliedConfigurations = _products.Values.SelectMany(pair => pair.AppliedConfigurations.Values);

            var cachedTargetFiles = new List<RcmCachedTargetFile>();
            var configStates = new List<RcmConfigState>();

            foreach (var cache in appliedConfigurations)
            {
                cachedTargetFiles.Add(new RcmCachedTargetFile(cache.Path.Path, cache.Length, cache.Hashes.Select(kp => new RcmCachedTargetFileHash(kp.Key, kp.Value)).ToList()));
                configStates.Add(new RcmConfigState(cache.Path.Id, cache.Version, cache.Path.Product));
            }

            var rcmState = new RcmClientState(_rootVersion, _targetsVersion, configStates, _lastPollError != null, _lastPollError);
            var rcmClient = new RcmClient(_id, _products.Keys, _rcmTracer, rcmState);
            var rcmRequest = new GetRcmRequest(rcmClient, cachedTargetFiles);

            return rcmRequest;
        }

        private void ProcessResponse(GetRcmResponse response)
        {
            var actualConfigPath =
                response
                   .ClientConfigs
                   .Select(RemoteConfigurationPath.FromPath)
                   .ToDictionary(path => path.Path);

            var changedConfigurationsByProduct = GetChangedConfigurations()
               .GroupBy(config => config.Path.Product);

            foreach (var productGroup in changedConfigurationsByProduct)
            {
                var product = _products[productGroup.Key];

                try
                {
                    var configurations = productGroup.ToList();

                    product.AssignConfigs(configurations);
                    CacheAppliedConfigurations(product, configurations);
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

                    if (!_products.TryGetValue(kp.Value.Product, out var product))
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

            void CacheAppliedConfigurations(Product product, List<RemoteConfiguration> configurations)
            {
                foreach (var config in configurations)
                {
                    var remoteConfigurationCache = new RemoteConfigurationCache(config.Path, config.Length, config.Hashes, config.Version);

                    if (product.AppliedConfigurations.ContainsKey(config.Path.Path))
                    {
                        product.AppliedConfigurations[config.Path.Path] = remoteConfigurationCache;
                    }
                    else
                    {
                        product.AppliedConfigurations.Add(config.Path.Path, remoteConfigurationCache);
                    }
                }
            }

            void UnapplyRemovedConfigurations()
            {
                foreach (var product in _products.Values)
                {
                    foreach (var appliedConfiguration in product.AppliedConfigurations)
                    {
                        if (!actualConfigPath.ContainsKey(appliedConfiguration.Key))
                        {
                            product.AppliedConfigurations.Remove(appliedConfiguration.Key);
                        }
                    }
                }
            }
        }

        public void OnShutdown()
        {
            _cancellationSource.Cancel();
        }
    }
}
