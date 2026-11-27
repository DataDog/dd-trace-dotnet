// <copyright file="RemoteConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
#if SHARED_REMOTE_CONFIG
using System.Linq;
#endif
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
#if SHARED_REMOTE_CONFIG
using Datadog.Trace.Agent.Transports;
#endif
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
#if SHARED_REMOTE_CONFIG
using Datadog.Trace.RemoteConfigurationManagement.Shared;
#endif
using Datadog.Trace.RemoteConfigurationManagement.Transport;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal sealed class RemoteConfigurationManager : IRemoteConfigurationManager
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RemoteConfigurationManager));

        private readonly IDiscoveryService _discoveryService;
        private readonly Action<AgentConfiguration> _discoveryCallback;
        private readonly object _discoveryGate = new();
        private readonly IGitMetadataTagsProvider _gitMetadataTagsProvider;
        private readonly TimeSpan _pollInterval;
        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly IDisposable _settingSubscription;
#if SHARED_REMOTE_CONFIG
        private readonly TaskCompletionSource<bool> _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly string? _nativeLibraryPath;
        private readonly object _workerGate = new();
        private CancellationTokenSource? _managedPollCancellation;
        private Task? _worker;
        private int _disposed;
#else
        private readonly TaskCompletionSource<bool> _processExit = new();
#endif

        private AgentTransport _transport;
        private AgentConfiguration? _agentConfiguration;
        private RcmClientTracer _rcmTracer;
        private int _isPollingStarted;

        private RemoteConfigurationManager(
            IDiscoveryService discoveryService,
            TracerSettings settings,
            TimeSpan pollInterval,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            IRcmSubscriptionManager subscriptionManager)
        {
            _discoveryService = discoveryService;
            _discoveryCallback = UpdateAgentConfiguration;
            _pollInterval = pollInterval;
            _gitMetadataTagsProvider = gitMetadataTagsProvider;

            _subscriptionManager = subscriptionManager;
#if SHARED_REMOTE_CONFIG
            if (subscriptionManager is RcmSubscriptionManager)
            {
                _nativeLibraryPath = AppContext.GetData(NativeAgentPoller.LibraryPathSetting) as string;
            }
#endif

            _transport = CreateTransport(settings.Manager.InitialExporterSettings);
            UpdateRcmClientTracer(settings.Manager.InitialMutableSettings);
            _settingSubscription = settings.Manager.SubscribeToChanges(changes =>
            {
#if SHARED_REMOTE_CONFIG
                if (changes.UpdatedMutable is { } updated
                 && (_nativeLibraryPath is null || HasRcmMetadataChanged(changes.PreviousMutable, updated)))
#else
                if (changes.UpdatedMutable is { } updated)
#endif
                {
                    UpdateRcmClientTracer(updated);
                }

                var exporter = changes.UpdatedExporter ?? changes.PreviousExporter;
                if (!ReferenceEquals(exporter, Volatile.Read(ref _transport).Exporter))
                {
                    Volatile.Write(ref _transport, CreateTransport(exporter));
                }
            });

            try
            {
                // Discovery replays synchronously. Order that replay before concurrent notifications.
                lock (_discoveryGate)
                {
                    discoveryService.SubscribeToChanges(_discoveryCallback);
                }
            }
            catch
            {
                _settingSubscription.Dispose();
                discoveryService.RemoveSubscription(_discoveryCallback);
                throw;
            }

            [MemberNotNull(nameof(_rcmTracer))]
            void UpdateRcmClientTracer(MutableSettings mutable)
            {
                var rcmTracer = RcmClientTracer.Create(
                    runtimeId: Util.RuntimeId.Get(),
                    tracerVersion: TracerConstants.SemanticVersion,
                    // Service Name must be lowercase, otherwise the agent will not be able to find the service
                    service: TraceUtil.NormalizeTag(mutable.DefaultServiceName),
                    env: TraceUtil.NormalizeTag(mutable.Environment),
                    appVersion: mutable.ServiceVersion,
                    globalTags: mutable.GlobalTags,
                    processTags: mutable.ProcessTags?.TagsList);
#if SHARED_REMOTE_CONFIG
                if (_nativeLibraryPath is not null)
                {
                    lock (_workerGate)
                    {
                        var previous = _rcmTracer;
                        var generation = previous?.TargetGeneration ?? 0;
                        if (previous is not null && !NativeAgentPoller.HasSameTarget(previous, rcmTracer))
                        {
                            generation++;
                        }

                        rcmTracer.TargetGeneration = generation;
                        Volatile.Write(ref _rcmTracer!, rcmTracer);
                    }

                    return;
                }
#endif
                Interlocked.Exchange(ref _rcmTracer!, rcmTracer);
            }

            AgentTransport CreateTransport(ExporterSettings exporter)
            {
#if SHARED_REMOTE_CONFIG
                if (_nativeLibraryPath is not null)
                {
                    return new AgentTransport(exporter, RemoteConfigurationApiFactory.CreateStreaming(exporter));
                }
#endif

                return new AgentTransport(exporter, RemoteConfigurationApiFactory.Create(exporter));
            }

#if SHARED_REMOTE_CONFIG
            static bool HasRcmMetadataChanged(MutableSettings previous, MutableSettings updated)
                => previous.DefaultServiceName != updated.DefaultServiceName
                   || previous.Environment != updated.Environment
                   || previous.ServiceVersion != updated.ServiceVersion
                   || previous.ProcessTags?.SerializedTags != updated.ProcessTags?.SerializedTags
                   || previous.GitCommitSha != updated.GitCommitSha
                   || previous.GitRepositoryUrl != updated.GitRepositoryUrl
                   || (!ReferenceEquals(previous.GlobalTags, updated.GlobalTags) && !previous.GlobalTags.SequenceEqual(updated.GlobalTags));
#endif
        }

        internal Task StopRequested => _processExit.Task;

#if SHARED_REMOTE_CONFIG
        internal bool UsesSharedClient => _nativeLibraryPath is not null;
#endif

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
                    subscriptionManager);
        }

        public void Start()
        {
#if SHARED_REMOTE_CONFIG
            lock (_workerGate)
            {
                if (_processExit.Task.IsCompleted || _worker is not null)
                {
                    return;
                }

                _worker = SchedulePolling();
            }
#else
            _ = SchedulePolling();
#endif
        }

        public void Dispose()
        {
#if SHARED_REMOTE_CONFIG
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                RequestStop();
#else
            if (_processExit.TrySetResult(true))
            {
#endif
                _discoveryService.RemoveSubscription(_discoveryCallback);
                _settingSubscription.Dispose();
            }
            else
            {
                // Double dispose in prod shouldn't happen, and should be avoided, so logging for follow-up
                Log.Debug($"{nameof(RemoteConfigurationManager)} is already disposed, skipping further disposal.");
            }
        }

#if SHARED_REMOTE_CONFIG
        internal void RequestStop() => _processExit.TrySetResult(true);

        internal Task StopAsync()
        {
            Dispose();
            lock (_workerGate)
            {
                return _worker ?? Task.CompletedTask;
            }
        }

        internal async Task RetireAsync()
        {
            CancellationTokenSource? cancellation;
            lock (_workerGate)
            {
                cancellation = _managedPollCancellation;
                _managedPollCancellation = null;
            }

            using (cancellation)
            {
                try
                {
                    cancellation?.Cancel();
                }
                finally
                {
                    await StopAsync().ConfigureAwait(false);
                }
            }
        }
#endif

        private Task SchedulePolling()
        {
#if SHARED_REMOTE_CONFIG
            var cancellation = new CancellationTokenSource();
            _managedPollCancellation = cancellation;
            var worker = Task.Run(async () =>
            {
                try
                {
                    await StartPollingAsync(cancellation.Token).ConfigureAwait(false);
                }
                finally
                {
                    lock (_workerGate)
                    {
                        if (ReferenceEquals(_managedPollCancellation, cancellation))
                        {
                            _managedPollCancellation = null;
                            cancellation.Dispose();
                        }
                    }
                }
            });
#else
            var worker = Task.Run(() => StartPollingAsync(default));
#endif
            _ = worker.ContinueWith(task => { Log.Error(task.Exception, "Remote Configuration management polling failed"); }, TaskContinuationOptions.OnlyOnFaulted);
            return worker;
        }

        private async Task StartPollingAsync(CancellationToken managedPollCancellation)
        {
            if (Interlocked.Exchange(ref _isPollingStarted, 1) != 0)
            {
                Log.Warning("Remote Configuration management polling is already started.");
                return;
            }

#if SHARED_REMOTE_CONFIG
            using var nativePoller = _nativeLibraryPath is { } path
                                         ? new NativeAgentPoller(path, (RcmSubscriptionManager)_subscriptionManager)
                                         : null;
#endif
            while (!_processExit.Task.IsCompleted)
            {
                var transport = Volatile.Read(ref _transport);
                if (GetConfigEndpoint(transport) is { Length: > 0 } endpoint && _subscriptionManager.HasAnySubscription)
                {
                    try
                    {
#if SHARED_REMOTE_CONFIG
                        if (nativePoller is not null && transport.Api is RemoteConfigurationApi<HttpClientRequest> streaming)
                        {
                            await PollNative(nativePoller, streaming, transport, endpoint).ConfigureAwait(false);
                        }
                        else
#endif
                        {
                            await Poll(transport, endpoint, managedPollCancellation).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
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

        private Task Poll(AgentTransport transport, string endpoint, CancellationToken cancellationToken)
        {
            var owner = ReferenceEquals(_subscriptionManager, RcmSubscriptionManager.Instance) ? TracerManager.Instance : null;
            if (owner is not null && !ReferenceEquals(owner.RemoteConfigurationManager, this))
            {
                return Task.CompletedTask;
            }

            var rcm = Volatile.Read(ref _rcmTracer);
            EnrichTagsWithGitMetadata(rcm);
            rcm.ExtraServices = ExtraServicesProvider.Instance.GetExtraServices();

            return _subscriptionManager.SendRequest(rcm, request => transport.Api.GetConfigs(request, endpoint, cancellationToken), AcceptResponse, CommitResponse);

            bool AcceptResponse() => !_processExit.Task.IsCompleted &&
                                     IsCurrentTransport(transport, endpoint) &&
                                     (owner is null || ReferenceEquals(owner, TracerManager.Instance));

            void CommitResponse(Action update)
            {
                if (owner is null)
                {
                    CommitIfCurrent();
                }
                else
                {
                    owner.TryRunWhileCurrent(CommitIfCurrent);
                }

                void CommitIfCurrent()
                {
                    if (AcceptResponse())
                    {
                        update();
                    }
                }
            }
        }

#if SHARED_REMOTE_CONFIG
        private Task PollNative(NativeAgentPoller poller, RemoteConfigurationApi<HttpClientRequest> api, AgentTransport transport, string endpoint)
        {
            var tracer = Volatile.Read(ref _rcmTracer);

            EnrichTagsWithGitMetadata(tracer);
            tracer.ExtraServices = ExtraServicesProvider.Instance.GetExtraServices();
            return poller.Poll(api, endpoint, tracer, _processExit.Task, IsCurrent);

            bool IsCurrent() => tracer.TargetGeneration == Volatile.Read(ref _rcmTracer).TargetGeneration &&
                                IsCurrentTransport(transport, endpoint);
        }
#endif

        private string? GetConfigEndpoint(AgentTransport transport)
            => Volatile.Read(ref _agentConfiguration) is { } configuration && ReferenceEquals(configuration.DiscoverySettings, transport.Exporter)
                   ? configuration.ConfigurationEndpoint
                   : null;

        private bool IsCurrentTransport(AgentTransport transport, string endpoint)
            => ReferenceEquals(transport, Volatile.Read(ref _transport)) &&
               string.Equals(endpoint, GetConfigEndpoint(transport), StringComparison.Ordinal);

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

        private void UpdateAgentConfiguration(AgentConfiguration configuration)
        {
            lock (_discoveryGate)
            {
                Volatile.Write(ref _agentConfiguration, configuration);
            }
        }

        private sealed class AgentTransport(ExporterSettings exporter, IRemoteConfigurationApi api)
        {
            public ExporterSettings Exporter { get; } = exporter;

            public IRemoteConfigurationApi Api { get; } = api;
        }
    }
}
