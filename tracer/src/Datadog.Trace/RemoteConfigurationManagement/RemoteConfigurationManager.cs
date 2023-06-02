// <copyright file="RemoteConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class RemoteConfigurationManager : IRemoteConfigurationManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RemoteConfigurationManager));

        private readonly string _id;
        private readonly RcmClientTracer _rcmTracer;
        private readonly IDiscoveryService _discoveryService;
        private readonly IRemoteConfigurationApi _remoteConfigurationApi;
        private readonly IGitMetadataTagsProvider _gitMetadataTagsProvider;
        private readonly TimeSpan _pollInterval;
        private readonly IRcmSubscriptionManager _subscriptionManager;

        private readonly CancellationTokenSource _cancellationSource;

        /// <summary>
        /// Key is the path
        /// </summary>
        private readonly Dictionary<string, RemoteConfigurationCache> _appliedConfigurations = new();

        private readonly int _rootVersion;
        private int _targetsVersion;
        private string? _lastPollError;
        private int _isPollingStarted;
        private bool _isRcmEnabled;
        private string? _backendClientState;
        private bool _gitMetadataAddedToRequestTags;

        private RemoteConfigurationManager(
            IDiscoveryService discoveryService,
            IRemoteConfigurationApi remoteConfigurationApi,
            string id,
            RcmClientTracer rcmTracer,
            TimeSpan pollInterval,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            IRcmSubscriptionManager subscriptionManager)
        {
            _discoveryService = discoveryService;
            _remoteConfigurationApi = remoteConfigurationApi;
            _rcmTracer = rcmTracer;
            _pollInterval = pollInterval;
            _gitMetadataTagsProvider = gitMetadataTagsProvider;
            _id = id;

            _rootVersion = 1;
            _targetsVersion = 0;
            _lastPollError = null;
            _subscriptionManager = subscriptionManager;
            _cancellationSource = new CancellationTokenSource();
            discoveryService.SubscribeToChanges(SetRcmEnabled);
        }

        public static RemoteConfigurationManager Create(IDiscoveryService discoveryService, IRemoteConfigurationApi remoteConfigurationApi, RemoteConfigurationSettings settings, string serviceName, ImmutableTracerSettings tracerSettings, IGitMetadataTagsProvider gitMetadataTagsProvider, IRcmSubscriptionManager subscriptionManager)
        {
            var tags = GetTags(settings, tracerSettings);

            return new RemoteConfigurationManager(
                    discoveryService,
                    remoteConfigurationApi,
                    id: settings.Id,
                    rcmTracer: new RcmClientTracer(settings.RuntimeId, settings.TracerVersion, serviceName, TraceUtil.NormalizeTag(tracerSettings.Environment), tracerSettings.ServiceVersion, tags),
                    pollInterval: settings.PollInterval,
                    gitMetadataTagsProvider,
                    subscriptionManager);
        }

        private static List<string> GetTags(RemoteConfigurationSettings rcmSettings, ImmutableTracerSettings tracerSettings)
        {
            var tags = tracerSettings.GlobalTags?.Select(pair => pair.Key + ":" + pair.Value).ToList() ?? new List<string>();

            var environment = TraceUtil.NormalizeTag(tracerSettings.Environment);
            if (!string.IsNullOrEmpty(environment))
            {
                tags.Add($"env:{environment}");
            }

            var serviceVersion = tracerSettings.ServiceVersion;
            if (!string.IsNullOrEmpty(serviceVersion))
            {
                tags.Add($"version:{serviceVersion}");
            }

            var tracerVersion = rcmSettings.TracerVersion;
            if (!string.IsNullOrEmpty(tracerVersion))
            {
                tags.Add($"tracer_version:{tracerVersion}");
            }

            var hostName = PlatformHelpers.HostMetadata.Instance?.Hostname;
            if (!string.IsNullOrEmpty(hostName))
            {
                tags.Add($"host_name:{hostName}");
            }

            return tags;
        }

        public void Start()
        {
            _ = Task.Run(StartPollingAsync)
               .ContinueWith(t => { Log.Error(t.Exception, "Remote Configuration management polling failed"); }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public void Dispose()
        {
            _discoveryService.RemoveSubscription(SetRcmEnabled);
            _cancellationSource.Cancel();
        }

        private async Task StartPollingAsync()
        {
            if (Interlocked.Exchange(ref _isPollingStarted, 1) != 0)
            {
                Log.Warning("Remote Configuration management polling is already started.");
                return;
            }

            while (!_cancellationSource.IsCancellationRequested)
            {
                var isRcmEnabled = Volatile.Read(ref _isRcmEnabled);

                if (isRcmEnabled && _subscriptionManager.HasAnySubscription)
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

        private async Task Poll()
        {
            try
            {
                var request = BuildRequest();
                var response = await _remoteConfigurationApi.GetConfigs(request).ConfigureAwait(false);

                if (response?.Targets?.Signed != null)
                {
                    ProcessResponse(response);
                    _targetsVersion = response.Targets.Signed.Version;
                    _backendClientState = response.Targets.Signed.Custom?.OpaqueBackendState;
                }
            }
            catch (Exception e)
            {
                _lastPollError = e.Message;
            }
        }

        private GetRcmRequest BuildRequest()
        {
            var cachedTargetFiles = new List<RcmCachedTargetFile>();
            var configStates = new List<RcmConfigState>();
            var appliedConfigs = _appliedConfigurations.Values;
            foreach (var cache in appliedConfigs)
            {
                cachedTargetFiles.Add(new RcmCachedTargetFile(cache.Path.Path, cache.Length, cache.Hashes.Select(kp => new RcmCachedTargetFileHash(kp.Key, kp.Value)).ToList()));
                configStates.Add(new RcmConfigState(cache.Path.Id, cache.Version, cache.Path.Product, cache.ApplyState, cache.Error));
            }

            var rcmState = new RcmClientState(_rootVersion, _targetsVersion, configStates, _lastPollError != null, _lastPollError, _backendClientState);
            var rcmClient = new RcmClient(_id, _subscriptionManager.ProductKeys, _rcmTracer, rcmState, _subscriptionManager.GetCapabilities());
            EnrichTagsWithGitMetadata(rcmClient.ClientTracer.Tags);
            var rcmRequest = new GetRcmRequest(rcmClient, cachedTargetFiles);
            return rcmRequest;
        }

        private void EnrichTagsWithGitMetadata(List<string> tags)
        {
            if (_gitMetadataAddedToRequestTags)
            {
                return;
            }

            if (!_gitMetadataTagsProvider.TryExtractGitMetadata(out var gitMetadata))
            {
                // no git metadata found, we can try again later.
                return;
            }

            if (gitMetadata != GitMetadata.Empty)
            {
                tags.Add($"{CommonTags.GitCommit}:{gitMetadata.CommitSha}");
                tags.Add($"{CommonTags.GitRepository}:{gitMetadata.RepositoryUrl}");
            }

            _gitMetadataAddedToRequestTags = true;
        }

        private void ProcessResponse(GetRcmResponse response)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                var description = response.TargetFiles.Count > 0
                                      ? "with the following paths: " + string.Join(",", response.TargetFiles.Select(t => t.Path))
                                      : "that is empty.";
                Log.Debug("Received Remote Configuration response {ResponseDescription}.", description);
            }

            var configByProducts = new Dictionary<string, List<RemoteConfiguration>>();
            var receivedPaths = new List<string>();

            // handle new configurations
            foreach (var clientConfig in response.ClientConfigs)
            {
                var remoteConfigurationPath = RemoteConfigurationPath.FromPath(clientConfig);
                receivedPaths.Add(remoteConfigurationPath.Path);
                var signed = response.Targets.Signed.Targets;
                var targetFiles =
                    (response.TargetFiles ?? Enumerable.Empty<RcmFile>()).ToDictionary(f => f.Path, f => f);

                if (!signed.TryGetValue(remoteConfigurationPath.Path, out var signedTarget))
                {
                    ThrowHelper.ThrowException($"Missing config {remoteConfigurationPath.Path} in targets");
                }

                if (!_subscriptionManager.ProductKeys.Contains(remoteConfigurationPath.Product))
                {
                    Log.Warning("Received config {RemoteConfigurationPath} for a product that was not requested", remoteConfigurationPath);
                    continue;
                }

                var isConfigApplied =
                    _appliedConfigurations.TryGetValue(remoteConfigurationPath.Path, out var appliedConfig) &&
                    appliedConfig.Hashes.SequenceEqual(signedTarget.Hashes);
                if (isConfigApplied)
                {
                    continue;
                }

                if (!targetFiles.TryGetValue(remoteConfigurationPath.Path, out var targetFile))
                {
                    ThrowHelper.ThrowException($"Missing config {remoteConfigurationPath.Path} in target files");
                }

                var remoteConfigurationCache = new RemoteConfigurationCache(remoteConfigurationPath, signedTarget.Length, signedTarget.Hashes, signedTarget.Custom.V);
                _appliedConfigurations[remoteConfigurationCache.Path.Path] = remoteConfigurationCache;

                var remoteConfiguration = new RemoteConfiguration(remoteConfigurationPath, targetFile.Raw, signedTarget.Length, signedTarget.Hashes, signedTarget.Custom.V);
                if (!configByProducts.ContainsKey(remoteConfigurationPath.Product))
                {
                    configByProducts[remoteConfigurationPath.Product] = new List<RemoteConfiguration>();
                }

                configByProducts[remoteConfigurationPath.Product].Add(remoteConfiguration);
            }

            Dictionary<string, List<RemoteConfigurationPath>> removedConfigsByProduct = new();
            // handle removed configurations
            foreach (var appliedConfiguration in _appliedConfigurations)
            {
                if (receivedPaths.Contains(appliedConfiguration.Key))
                {
                    continue;
                }

                if (!removedConfigsByProduct.ContainsKey(appliedConfiguration.Value.Path.Product))
                {
                    removedConfigsByProduct[appliedConfiguration.Value.Path.Product] =
                        new List<RemoteConfigurationPath>();
                }

                removedConfigsByProduct[appliedConfiguration.Value.Path.Product].Add(appliedConfiguration.Value.Path);
            }

            // update applied configurations after removal
            foreach (var removedConfig in removedConfigsByProduct.Values)
            {
                foreach (var value in removedConfig)
                {
                    _appliedConfigurations.Remove(value.Path);
                }
            }

            var results = _subscriptionManager.Update(configByProducts, removedConfigsByProduct);

            if (results != null)
            {
                foreach (var result in results)
                {
                    switch (result.ApplyState)
                    {
                        case ApplyStates.UNACKNOWLEDGED:
                            // Do nothing
                            break;
                        case ApplyStates.ACKNOWLEDGED:
                            _appliedConfigurations[result.Filename].Applied();
                            break;
                        case ApplyStates.ERROR:
                            _appliedConfigurations[result.Filename].ErrorOccured(result.Error);
                            break;
                        default:
                            Log.Warning("Unexpected ApplyState: {ApplyState}", result.ApplyState);
                            break;
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
