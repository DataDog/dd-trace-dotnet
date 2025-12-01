// <copyright file="RemoteConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        private readonly IDiscoveryService _discoveryService;
        private readonly IGitMetadataTagsProvider _gitMetadataTagsProvider;
        private readonly TimeSpan _pollInterval;
        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly IDisposable _settingSubscription;
        private readonly TaskCompletionSource<bool> _processExit = new();

        private IRemoteConfigurationApi _remoteConfigurationApi;
        private RcmClientTracer _rcmTracer;
        private int _isPollingStarted;
        private bool _isRcmEnabled;

        private RemoteConfigurationManager(
            IDiscoveryService discoveryService,
            TracerSettings settings,
            TimeSpan pollInterval,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            IRcmSubscriptionManager subscriptionManager,
            List<string>? processTags)
        {
            _discoveryService = discoveryService;
            _pollInterval = pollInterval;
            _gitMetadataTagsProvider = gitMetadataTagsProvider;

            _subscriptionManager = subscriptionManager;
            discoveryService.SubscribeToChanges(SetRcmEnabled);
            UpdateRcmApi(settings.Manager.InitialExporterSettings);
            UpdateRcmClientTracer(settings.Manager.InitialMutableSettings);
            _settingSubscription = settings.Manager.SubscribeToChanges(changes =>
            {
                if (changes.UpdatedMutable is { } updated)
                {
                    UpdateRcmClientTracer(updated);
                }

                if (changes.UpdatedExporter is { } exporter)
                {
                    UpdateRcmApi(exporter);
                }
            });

            [MemberNotNull(nameof(_rcmTracer))]
            void UpdateRcmClientTracer(MutableSettings mutable)
            {
                var rcmTracer = RcmClientTracer.Create(
                    runtimeId: Util.RuntimeId.Get(),
                    tracerVersion: TracerConstants.ThreePartVersion,
                    // Service Name must be lowercase, otherwise the agent will not be able to find the service
                    service: TraceUtil.NormalizeTag(mutable.DefaultServiceName),
                    env: TraceUtil.NormalizeTag(mutable.Environment),
                    appVersion: mutable.ServiceVersion,
                    globalTags: mutable.GlobalTags,
                    processTags: processTags);
                Interlocked.Exchange(ref _rcmTracer!, rcmTracer);
            }

            [MemberNotNull(nameof(_remoteConfigurationApi))]
            void UpdateRcmApi(ExporterSettings exporter)
            {
                var rcmApi = RemoteConfigurationApiFactory.Create(exporter, discoveryService);

                Interlocked.Exchange(ref _remoteConfigurationApi!, rcmApi);
            }
        }

        public static RemoteConfigurationManager Create(
            IDiscoveryService discoveryService,
            RemoteConfigurationSettings settings,
            TracerSettings tracerSettings,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            IRcmSubscriptionManager subscriptionManager)
        {
            return new RemoteConfigurationManager(
                    discoveryService,
                    tracerSettings,
                    pollInterval: settings.PollInterval,
                    gitMetadataTagsProvider,
                    subscriptionManager,
                    tracerSettings.PropagateProcessTags ? ProcessTags.TagsList : null);
        }

        public void Start()
        {
            _ = Task.Run(StartPollingAsync)
               .ContinueWith(t => { Log.Error(t.Exception, "Remote Configuration management polling failed"); }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public void Dispose()
        {
            if (_processExit.TrySetResult(true))
            {
                _discoveryService.RemoveSubscription(SetRcmEnabled);
                _settingSubscription.Dispose();
            }
            else
            {
                // Double dispose in prod shouldn't happen, and should be avoided, so logging for follow-up
                Log.Debug($"{nameof(RemoteConfigurationManager)} is already disposed, skipping further disposal.");
            }
        }

        private async Task StartPollingAsync()
        {
            if (Interlocked.Exchange(ref _isPollingStarted, 1) != 0)
            {
                Log.Warning("Remote Configuration management polling is already started.");
                return;
            }

            while (!_processExit.Task.IsCompleted)
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
                    await Task.WhenAny(_processExit.Task, Task.Delay(_pollInterval)).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // We are shutting down, so don't do anything about it
                }
            }
        }

        private Task Poll()
        {
            var rcm = Volatile.Read(ref _rcmTracer);
            return _subscriptionManager.SendRequest(rcm, request =>
            {
                EnrichTagsWithGitMetadata(request.Client.ClientTracer);
                request.Client.ClientTracer.ExtraServices = ExtraServicesProvider.Instance.GetExtraServices();

                return Volatile.Read(ref _remoteConfigurationApi).GetConfigs(request);
            });
        }

        private void EnrichTagsWithGitMetadata(RcmClientTracer details)
        {
            if (details.IsGitMetadataAddedToRequestTags)
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
                details.Tags.Add($"{CommonTags.GitCommit}:{gitMetadata.CommitSha}");
                details.Tags.Add($"{CommonTags.GitRepository}:{gitMetadata.RepositoryUrl}");
            }

            details.IsGitMetadataAddedToRequestTags = true;
        }

        private void SetRcmEnabled(AgentConfiguration c)
        {
            _isRcmEnabled = !string.IsNullOrEmpty(c.ConfigurationEndpoint);
        }
    }
}
