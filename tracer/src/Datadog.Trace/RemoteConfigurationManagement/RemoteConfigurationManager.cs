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

namespace Datadog.Trace.RemoteConfigurationManagement;

internal class RemoteConfigurationManager : IRemoteConfigurationManager
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RemoteConfigurationManager));
    private static readonly object GlobalLock = new();

    private readonly string _id;
    private readonly string _runtimeId;
    private readonly string _tracerVersion;
    private readonly string _service;
    private readonly string _env;
    private readonly string _appVersion;
    private readonly IDiscoveryService _discoveryService;
    private readonly IRemoteConfigurationApi _remoteConfigurationApi;
    private readonly TimeSpan _pollInterval;

    private readonly CancellationTokenSource _cancellationSource;
    private readonly ConcurrentDictionary<string, Product> _products;
    private readonly object _instanceLock = new();

    private int _rootVersion;
    private int _targetsVersion;
    private string _lastPollError;
    private bool _isPollingStarted;

    private RemoteConfigurationManager(
        IDiscoveryService discoveryService,
        IRemoteConfigurationApi remoteConfigurationApi,
        string id,
        string runtimeId,
        string tracerVersion,
        string service,
        string env,
        string appVersion,
        TimeSpan pollInterval)
    {
        _discoveryService = discoveryService;
        _remoteConfigurationApi = remoteConfigurationApi;
        _runtimeId = runtimeId;
        _tracerVersion = tracerVersion;
        _service = service;
        _env = env;
        _appVersion = appVersion;
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
        lock (GlobalLock)
        {
            return
                Instance ??= new RemoteConfigurationManager(
                    discoveryService,
                    remoteConfigurationApi,
                    id: settings.Id,
                    runtimeId: settings.RuntimeId,
                    tracerVersion: settings.TracerVersion,
                    service: serviceName,
                    env: settings.Environment,
                    appVersion: settings.AppVersion,
                    pollInterval: settings.PollInterval);
        }
    }

    public async Task StartPollingAsync()
    {
        lock (GlobalLock)
        {
            if (_isPollingStarted)
            {
                Log.Warning("Remote Configuration management polling is already started.");
                return;
            }

            _isPollingStarted = true;
        }

        LifetimeManager.Instance.AddShutdownTask(OnShutdown);

        while (!_cancellationSource.IsCancellationRequested)
        {
            try
            {
                if (!string.IsNullOrEmpty(_discoveryService.ConfigurationEndpoint))
                {
                    _lastPollError = null;
                    Poll();
                }
            }
            catch (Exception e)
            {
                _lastPollError = e.Message;
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

    private void Poll()
    {
        lock (_instanceLock)
        {
            var request = BuildRequest();
            var response = _remoteConfigurationApi.GetConfigs(request).Result;

            if (response?.Targets?.Signed != null)
            {
                ProcessResponse(response);
                _targetsVersion = response.Targets.Signed.Version;
            }
        }
    }

    private GetRcmRequest BuildRequest()
    {
        var appliedConfigurations = _products.Values.SelectMany(pair => pair.AppliedConfigurations.Values).ToList();
        var cachedTargetFiles = appliedConfigurations.Select(cache => new RcmCachedTargetFile(cache.Path.Path, cache.Length, cache.Hashes.Select(kp => new RcmCachedTargetFileHash(kp.Key, kp.Value)).ToList())).ToList();

        return new GetRcmRequest(BuildRequestClient(), cachedTargetFiles);

        RcmClient BuildRequestClient()
        {
            return new RcmClient(_id, _products.Keys.ToList(), BuildRcmClientTracer(), BuildRequestClientState());
        }

        RcmClientTracer BuildRcmClientTracer()
        {
            return new RcmClientTracer(_runtimeId, _tracerVersion, _service, _env, _appVersion);
        }

        RcmClientState BuildRequestClientState()
        {
            var configStates = appliedConfigurations.Select(cache => new RcmConfigState(cache.Path.Id, cache.Version, cache.Path.Product)).ToList();
            return new RcmClientState(_rootVersion, _targetsVersion, configStates, _lastPollError != null, _lastPollError);
        }
    }

    private void ProcessResponse(GetRcmResponse response)
    {
        var configsToApply = (response.TargetFiles ?? Enumerable.Empty<RcmFile>()).ToDictionary(f => f.Path, f => f);
        var configsToVerify = response.Targets.Signed.Targets;

        var changedConfigurations = new List<RemoteConfiguration>();
        foreach (var path in response.ClientConfigs)
        {
            var configPath = RemoteConfigurationPath.FromPath(path);
            if (!configsToVerify.TryGetValue(path, out var signedTarget))
            {
                throw new RemoteConfigurationException($"Missing config {path} in targets");
            }

            if (!_products.TryGetValue(configPath.Product, out var product))
            {
                throw new RemoteConfigurationException($"Received config {path} for a product that was not requested");
            }

            var isConfigApplied = product.AppliedConfigurations.TryGetValue(path, out var appliedConfig) && appliedConfig.Hashes.SequenceEqual(signedTarget.Hashes);
            if (isConfigApplied)
            {
                continue;
            }

            if (!configsToApply.ContainsKey(path))
            {
                throw new RemoteConfigurationException($"Missing config {path} in target files");
            }

            var config = new RemoteConfiguration(configPath, configsToApply[path].Raw, signedTarget.Length, signedTarget.Hashes, signedTarget.Custom.V);
            changedConfigurations.Add(config);
        }

        var configsByProduct = changedConfigurations.GroupBy(config => config.Path.Product);
        foreach (var productGroup in configsByProduct)
        {
            var configurations = productGroup.ToList();
            var product = _products[productGroup.Key];

            product.AssignConfigs(configurations);
            foreach (var config in product.AppliedConfigurations)
            {
                if (!configsToVerify.ContainsKey(config.Key))
                {
                    product.AppliedConfigurations.Remove(config.Key);
                }
            }

            foreach (var config in configurations)
            {
                product.AppliedConfigurations.Add(config.Path.Path, new RemoteConfigurationCache(config.Path, config.Length, config.Hashes, config.Version));
            }
        }
    }

    public void OnShutdown()
    {
        _cancellationSource.Cancel();
    }
}
