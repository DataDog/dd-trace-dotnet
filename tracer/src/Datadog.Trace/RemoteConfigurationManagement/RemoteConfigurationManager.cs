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

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal class RemoteConfigurationManager : IRemoteConfigurationManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RemoteConfigurationManager));

        private readonly RcmClientTracer _rcmTracer;
        private readonly IDiscoveryService _discoveryService;
        private readonly IRemoteConfigurationApi _remoteConfigurationApi;
        private readonly IGitMetadataTagsProvider _gitMetadataTagsProvider;
        private readonly TimeSpan _pollInterval;
        private readonly IRcmSubscriptionManager _subscriptionManager;

        private readonly CancellationTokenSource _cancellationSource;

        private int _isPollingStarted;
        private bool _isRcmEnabled;
        private bool _gitMetadataAddedToRequestTags;

        private RemoteConfigurationManager(
            IDiscoveryService discoveryService,
            IRemoteConfigurationApi remoteConfigurationApi,
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

            _subscriptionManager = subscriptionManager;
            _cancellationSource = new CancellationTokenSource();
            discoveryService.SubscribeToChanges(SetRcmEnabled);
        }

        public static RemoteConfigurationManager Create(
            IDiscoveryService discoveryService,
            IRemoteConfigurationApi remoteConfigurationApi,
            RemoteConfigurationSettings settings,
            string serviceName,
            ImmutableTracerSettings tracerSettings,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            IRcmSubscriptionManager subscriptionManager)
        {
            var tags = GetTags(settings, tracerSettings);

            return new RemoteConfigurationManager(
                    discoveryService,
                    remoteConfigurationApi,
                    rcmTracer: new RcmClientTracer(settings.RuntimeId, settings.TracerVersion, serviceName, TraceUtil.NormalizeTag(tracerSettings.EnvironmentInternal), tracerSettings.ServiceVersionInternal, tags),
                    pollInterval: settings.PollInterval,
                    gitMetadataTagsProvider,
                    subscriptionManager);
        }

        private static List<string> GetTags(RemoteConfigurationSettings rcmSettings, ImmutableTracerSettings tracerSettings)
        {
            var tags = tracerSettings.GlobalTagsInternal?.Select(pair => pair.Key + ":" + pair.Value).ToList() ?? new List<string>();

            var environment = TraceUtil.NormalizeTag(tracerSettings.EnvironmentInternal);
            if (!string.IsNullOrEmpty(environment))
            {
                tags.Add($"env:{environment}");
            }

            var serviceVersion = tracerSettings.ServiceVersionInternal;
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
                    try
                    {
                        await Poll().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // It shouldn't happen because the RcmSubscriptionManager swallows exceptions.
                        // But hey, we all know what's going to happen sooner or later.
                        Log.Error(ex, "Error while polling remote configuration management service");
                    }
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

        private Task Poll()
        {
            return _subscriptionManager.SendRequest(_rcmTracer, request =>
            {
                EnrichTagsWithGitMetadata(request.Client.ClientTracer.Tags);
                request.Client.ClientTracer.ExtraServices = ExtraServicesProvider.Instance.GetExtraServices();

                return _remoteConfigurationApi.GetConfigs(request);
            });
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

        private void SetRcmEnabled(AgentConfiguration c)
        {
            _isRcmEnabled = !string.IsNullOrEmpty(c.ConfigurationEndpoint);
        }
    }
}
