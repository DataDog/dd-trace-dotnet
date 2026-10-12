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

        // Only accessed by the polling thread, except for cancellation
        private IRemoteConfigClient? _client;
        private RcmClientTracer? _clientTracer;
        private RemoteConfigEndpoint? _clientEndpoint;
        private string[]? _clientExtraServices;

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
            _ = Task.Factory.StartNew(PollLoop, TaskCreationOptions.LongRunning)
               .ContinueWith(t => { Log.Error(t.Exception, "Remote Configuration management polling failed"); }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
            {
                _processExit.Set();
                _discoveryService.RemoveSubscription(SetRcmEnabled);
                _settingSubscription.Dispose();
                // Don't wait for a pending request, e.g. when the AppDomain is unloaded
                Volatile.Read(ref _client)?.Cancel();
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

            try
            {
                while (!_processExit.IsSet)
                {
                    var isRcmEnabled = Volatile.Read(ref _isRcmEnabled);

                    if (isRcmEnabled && _subscriptionManager.HasAnySubscription)
                    {
                        try
                        {
                            if (!Poll())
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
            finally
            {
                var client = Interlocked.Exchange(ref _client, null);
                client?.Dispose();
            }
        }

        /// <summary>
        /// Returns false if remote configuration is unavailable.
        /// </summary>
        private bool Poll()
        {
            var tracer = Volatile.Read(ref _rcmTracer);
            var endpoint = Volatile.Read(ref _endpoint);
            var gitMetadataAdded = EnrichTagsWithGitMetadata(tracer);

            var client = _client;
            if (client is null)
            {
                try
                {
                    client = _clientFactory!(_subscriptionManager.ClientId, tracer, endpoint);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Remote Configuration is unavailable: failed to create the remote configuration client");
                    return false;
                }

                Volatile.Write(ref _client, client);
            }
            else if (gitMetadataAdded || !ReferenceEquals(tracer, _clientTracer) || !ReferenceEquals(endpoint, _clientEndpoint))
            {
                client.Rebind(tracer, ReferenceEquals(endpoint, _clientEndpoint) ? null : endpoint);
            }

            _clientTracer = tracer;
            _clientEndpoint = endpoint;

            var extraServices = ExtraServicesProvider.Instance.GetExtraServices();
            if (!ReferenceEquals(extraServices, _clientExtraServices))
            {
                client.SetExtraServices(extraServices);
                _clientExtraServices = extraServices;
            }

            _subscriptionManager.Poll(client);
            return true;
        }

        /// <summary>
        /// Adds the git metadata tags once available, returns whether they were just added.
        /// </summary>
        private bool EnrichTagsWithGitMetadata(RcmClientTracer details)
        {
            if (details.IsGitMetadataAddedToRequestTags)
            {
                return false;
            }

            if (!_gitMetadataTagsProvider.TryExtractGitMetadata(out var gitMetadata))
            {
                // no git metadata found, we can try again later.
                return false;
            }

            if (gitMetadata != GitMetadata.Empty)
            {
                details.Tags.Add($"{CommonTags.GitCommit}:{gitMetadata.CommitSha}");
                details.Tags.Add($"{CommonTags.GitRepository}:{gitMetadata.RepositoryUrl}");
            }

            details.IsGitMetadataAddedToRequestTags = true;
            return gitMetadata != GitMetadata.Empty;
        }

        private void SetRcmEnabled(AgentConfiguration c)
        {
            _isRcmEnabled = !string.IsNullOrEmpty(c.ConfigurationEndpoint);
        }
    }
}
