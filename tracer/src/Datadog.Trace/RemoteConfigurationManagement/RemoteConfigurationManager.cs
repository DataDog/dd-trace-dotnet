// <copyright file="RemoteConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.LibDatadog;
using Datadog.Trace.LibDatadog.RemoteConfig;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal sealed class RemoteConfigurationManager : IRemoteConfigurationManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RemoteConfigurationManager));

        private readonly IDiscoveryService _discoveryService;
        private readonly IGitMetadataTagsProvider _gitMetadataTagsProvider;
        private readonly TimeSpan _pollInterval;
        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly Func<string, RcmClientTracer, RemoteConfigEndpoint, IRemoteConfigClient>? _clientFactory;
        private readonly IDisposable _settingSubscription;
        private readonly ManualResetEventSlim _processExit = new();

        private RcmClientTracer _rcmTracer;
        private RemoteConfigEndpoint _endpoint;
        private int _isPollingStarted;
        private int _isDisposed;
        private bool _isRcmEnabled;

        private RemoteConfigurationManager(
            IDiscoveryService discoveryService,
            TracerSettings settings,
            TimeSpan pollInterval,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            IRcmSubscriptionManager subscriptionManager,
            Func<string, RcmClientTracer, RemoteConfigEndpoint, IRemoteConfigClient>? clientFactory)
        {
            _discoveryService = discoveryService;
            _pollInterval = pollInterval;
            _gitMetadataTagsProvider = gitMetadataTagsProvider;
            _clientFactory = clientFactory;

            _subscriptionManager = subscriptionManager;
            discoveryService.SubscribeToChanges(SetRcmEnabled);
            UpdateEndpoint(settings.Manager.InitialExporterSettings);
            UpdateRcmClientTracer(settings.Manager.InitialMutableSettings);
            _settingSubscription = settings.Manager.SubscribeToChanges(changes =>
            {
                if (changes.UpdatedMutable is { } updated)
                {
                    UpdateRcmClientTracer(updated);
                }

                if (changes.UpdatedExporter is { } exporter)
                {
                    UpdateEndpoint(exporter);
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
                    processTags: mutable.ProcessTags?.TagsList);
                Interlocked.Exchange(ref _rcmTracer!, rcmTracer);
            }

            [MemberNotNull(nameof(_endpoint))]
            void UpdateEndpoint(ExporterSettings exporter)
            {
                Interlocked.Exchange(ref _endpoint!, RemoteConfigEndpoint.FromExporterSettings(exporter));
            }
        }

        public static RemoteConfigurationManager Create(
            IDiscoveryService discoveryService,
            RemoteConfigurationSettings settings,
            TracerSettings tracerSettings,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            IRcmSubscriptionManager subscriptionManager,
            Func<string, RcmClientTracer, RemoteConfigEndpoint, IRemoteConfigClient>? clientFactory = null)
        {
            // The remote configuration client is part of libdatadog
            if (clientFactory is null && LibDatadogAvailabilityHelper.IsLibDatadogAvailable.IsAvailable)
            {
                clientFactory = NativeRemoteConfigClient.Create;
            }

            return new RemoteConfigurationManager(
                    discoveryService,
                    tracerSettings,
                    pollInterval: settings.PollInterval,
                    gitMetadataTagsProvider,
                    subscriptionManager,
                    clientFactory);
        }

        public void Start()
        {
            // The libdatadog client blocks while polling, so poll on a dedicated thread
            _ = Task.Factory.StartNew(PollLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
               .ContinueWith(t => { Log.Error(t.Exception, "Remote Configuration management polling failed"); }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                // The client is shared with the remote configuration manager of a replacing tracer, so it is only cancelled on shutdown
                _processExit.Set();
                _discoveryService.RemoveSubscription(SetRcmEnabled);
                _settingSubscription.Dispose();
            }
            else
            {
                // Double dispose in prod shouldn't happen, and should be avoided, so logging for follow-up
                Log.Debug($"{nameof(RemoteConfigurationManager)} is already disposed, skipping further disposal.");
            }
        }

        private void PollLoop()
        {
            if (Interlocked.Exchange(ref _isPollingStarted, 1) != 0)
            {
                Log.Warning("Remote Configuration management polling is already started.");
                return;
            }

            if (_clientFactory is null)
            {
                Log.Information("Remote Configuration is unavailable, as libdatadog is not available.");
                return;
            }

            while (!_processExit.IsSet)
            {
                var isRcmEnabled = Volatile.Read(ref _isRcmEnabled);

                if (isRcmEnabled && _subscriptionManager.HasAnySubscription)
                {
                    try
                    {
                        var tracer = EnrichTagsWithGitMetadata(Volatile.Read(ref _rcmTracer));
                        if (!_subscriptionManager.Poll(_clientFactory, tracer, Volatile.Read(ref _endpoint), ExtraServicesProvider.Instance.GetExtraServices()))
                        {
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error while polling remote configuration management service");
                    }
                }

                _processExit.Wait(_pollInterval);
            }
        }

        /// <summary>
        /// Returns <paramref name="tracer"/> with the git metadata tags once available. A new instance, as the client
        /// is only rebound for new ones.
        /// </summary>
        private RcmClientTracer EnrichTagsWithGitMetadata(RcmClientTracer tracer)
        {
            if (tracer.IsGitMetadataAddedToRequestTags || !_gitMetadataTagsProvider.TryExtractGitMetadata(out var gitMetadata))
            {
                // already added, or no git metadata found yet, we can try again later.
                return tracer;
            }

            var enriched = gitMetadata == GitMetadata.Empty
                               ? tracer
                               : new RcmClientTracer(
                                   tracer.RuntimeId!,
                                   tracer.TracerVersion!,
                                   tracer.Service!,
                                   tracer.Env!,
                                   tracer.AppVersion,
                                   [.. tracer.Tags, $"{CommonTags.GitCommit}:{gitMetadata.CommitSha}", $"{CommonTags.GitRepository}:{gitMetadata.RepositoryUrl}"],
                                   tracer.ProcessTags);
            enriched.IsGitMetadataAddedToRequestTags = true;
            Interlocked.CompareExchange(ref _rcmTracer, enriched, tracer);
            return enriched;
        }

        private void SetRcmEnabled(AgentConfiguration c)
        {
            _isRcmEnabled = !string.IsNullOrEmpty(c.ConfigurationEndpoint);
        }
    }
}
