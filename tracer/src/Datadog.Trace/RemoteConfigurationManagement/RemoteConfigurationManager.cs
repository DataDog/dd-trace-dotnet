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
        private readonly TracerSettings.SettingsManager _settings;
        private readonly IGitMetadataTagsProvider _gitMetadataTagsProvider;
        private readonly TimeSpan _pollInterval;
        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly IDisposable _settingSubscription;
        private readonly TaskCompletionSource<bool> _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _workerGate = new();
#if SHARED_REMOTE_CONFIG
        private readonly string? _nativeLibraryPath;
#endif
        private CancellationTokenSource? _managedPollCancellation;
        private Task? _worker;
        private TracerManager? _owner;
        private int _disposed;

        private ManagedRcmPoll? _managedPoll;
        private AgentTransport _transport;
        private RcmClientTracer _rcmTracer;

        private RemoteConfigurationManager(
            IDiscoveryService discoveryService,
            TracerSettings settings,
            RemoteConfigurationSettings remoteConfigurationSettings,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            IRcmSubscriptionManager subscriptionManager)
        {
            _discoveryService = discoveryService;
            _settings = settings.Manager;
            _pollInterval = remoteConfigurationSettings.PollInterval;
            _gitMetadataTagsProvider = gitMetadataTagsProvider;

            _subscriptionManager = subscriptionManager;
#if SHARED_REMOTE_CONFIG
            if (subscriptionManager is RcmSubscriptionManager)
            {
                _nativeLibraryPath = remoteConfigurationSettings.NativeLibraryPath;
            }
#endif

            _transport = CreateTransport(_settings.InitialExporterSettings);
            var mutableSettings = _settings.InitialMutableSettings;
            UpdateRcmClientTracer(mutableSettings, targetGeneration: 0);
            _settingSubscription = _settings.SubscribeToChanges(changes =>
            {
                var updated = changes.EffectiveMutable;
                var targetGeneration = changes.RemoteConfigurationTargetGeneration;
                var targetChanged = targetGeneration != _rcmTracer.TargetGeneration;
                if (targetChanged || !ReferenceEquals(mutableSettings, updated))
                {
#if SHARED_REMOTE_CONFIG
                    if (targetChanged || _nativeLibraryPath is null || HasRcmMetadataChanged(mutableSettings, updated))
#endif
                    {
                        UpdateRcmClientTracer(updated, targetGeneration);
                    }

                    mutableSettings = updated;
                }

                var exporter = changes.EffectiveExporter;
                if (!ReferenceEquals(exporter, Volatile.Read(ref _transport).Exporter))
                {
                    Volatile.Write(ref _transport, CreateTransport(exporter));
                }
            });

            [MemberNotNull(nameof(_rcmTracer))]
            void UpdateRcmClientTracer(MutableSettings mutable, long targetGeneration)
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
                rcmTracer.TargetGeneration = targetGeneration;
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
                => previous.GitCommitSha != updated.GitCommitSha
                   || previous.GitRepositoryUrl != updated.GitRepositoryUrl
                   || (!ReferenceEquals(previous.GlobalTags, updated.GlobalTags) && !previous.GlobalTags.SequenceEqual(updated.GlobalTags));
#endif
        }

        public bool IsEnabled => true;

        public Task StopRequested => _processExit.Task;

        private bool RequiresProcessExitDrain
        {
            get
            {
#if SHARED_REMOTE_CONFIG
                return _nativeLibraryPath is not null && _worker is not null;
#else
                return false;
#endif
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
                    settings,
                    gitMetadataTagsProvider,
                    subscriptionManager);
        }

        public void Start(TracerManager? owner)
        {
            lock (_workerGate)
            {
                if (_processExit.Task.IsCompleted || owner is { IsCurrent: false })
                {
                    return;
                }

                if (owner is not null)
                {
                    Volatile.Write(ref _owner, owner);
                }

                _worker ??= SchedulePolling();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                RequestStop();
                _settingSubscription.Dispose();
            }
            else
            {
                // Double dispose in prod shouldn't happen, and should be avoided, so logging for follow-up
                Log.Debug($"{nameof(RemoteConfigurationManager)} is already disposed, skipping further disposal.");
            }
        }

        public Func<Task> PrepareRetirement(out bool drainAtProcessExit)
        {
            lock (_workerGate)
            {
                RequestStop();
                drainAtProcessExit = RequiresProcessExitDrain;
                return RetireAsync;
            }
        }

        public Func<Task>? PrepareShutdownDrain()
        {
            lock (_workerGate)
            {
                if (!RequiresProcessExitDrain)
                {
                    return null;
                }

                RequestStop();
                return StopAsync;
            }
        }

        internal void Start() => Start(null);

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

        private void RequestStop() => _processExit.TrySetResult(true);

        private Task SchedulePolling()
        {
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
            _ = worker.ContinueWith(task => { Log.Error(task.Exception, "Remote Configuration management polling failed"); }, TaskContinuationOptions.OnlyOnFaulted);
            return worker;
        }

        private async Task StartPollingAsync(CancellationToken managedPollCancellation)
        {
#if SHARED_REMOTE_CONFIG
            using var nativePoller = _nativeLibraryPath is { } path
                                         ? new NativeAgentPoller(path, (RcmSubscriptionManager)_subscriptionManager)
                                         : null;
#endif
            while (!_processExit.Task.IsCompleted)
            {
                var owner = Volatile.Read(ref _owner);
                var transport = Volatile.Read(ref _transport);
                var tracer = Volatile.Read(ref _rcmTracer);
                if (IsPollOwnerCurrent(owner)
                 && tracer.TargetGeneration == _settings.CurrentRemoteConfigurationTargetGeneration
                 && _discoveryService.CurrentConfiguration is { } configuration
                 && GetConfigEndpoint(transport, configuration) is { Length: > 0 } endpoint
                 && _subscriptionManager.HasAnySubscription)
                {
                    try
                    {
                        var context = new PollContext(owner, transport, endpoint, configuration.RemoteConfigurationEndpointGeneration, tracer.TargetGeneration);
                        EnrichTagsWithGitMetadata(tracer);
                        tracer.ExtraServices = ExtraServicesProvider.Instance.GetExtraServices();
#if SHARED_REMOTE_CONFIG
                        if (nativePoller is not null)
                        {
                            await PollNative(nativePoller, context, tracer).ConfigureAwait(false);
                        }
                        else
#endif
                        {
                            await Poll(context, tracer, managedPollCancellation).ConfigureAwait(false);
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

        private Task Poll(PollContext context, RcmClientTracer tracer, CancellationToken cancellationToken)
        {
            var poll = _managedPoll ??= _subscriptionManager.CreateManagedPoller();
            return poll(tracer, request => context.Transport.Api.GetConfigs(request, context.Endpoint, cancellationToken), AcceptResponse, CommitResponse);

            bool AcceptResponse() => IsPollCurrent(context, _discoveryService.CurrentConfiguration);

            void CommitResponse(Action update) => TryCommitResponse(context, () =>
            {
                update();
                return true;
            });
        }

#if SHARED_REMOTE_CONFIG
        private Task PollNative(NativeAgentPoller poller, PollContext context, RcmClientTracer tracer)
        {
            return poller.Poll(context.Transport.Api, context.Endpoint, tracer, _processExit.Task, IsCurrent, TryCommit);

            bool IsCurrent() => IsPollCurrent(context, _discoveryService.CurrentConfiguration);

            bool TryCommit(Func<bool> publish) => TryCommitResponse(context, publish);
        }
#endif

        private bool IsPollOwnerCurrent(TracerManager? owner)
            => !_processExit.Task.IsCompleted && ReferenceEquals(Volatile.Read(ref _owner), owner) && (owner is null || owner.IsCurrent);

        private bool TryCommitResponse(PollContext context, Func<bool> publish)
        {
            return context.Owner is { } owner ? owner.TryRunWhileCurrent(CommitCurrentRoute) : CommitCurrentRoute();

            bool CommitCurrentRoute()
                => _settings.TryRunWithCurrentAgentConfiguration(
                    context.Transport.Exporter,
                    _discoveryService,
                    configuration => IsPollCurrent(context, configuration) && publish());
        }

        private string? GetConfigEndpoint(AgentTransport transport, AgentConfiguration? configuration)
            => configuration is not null &&
               ReferenceEquals(configuration.DiscoverySettings, transport.Exporter) &&
               ReferenceEquals(transport.Exporter, _settings.CurrentExporterSettings)
                   ? configuration.ConfigurationEndpoint
                   : null;

        private bool IsPollCurrent(PollContext context, AgentConfiguration? configuration)
            => IsPollOwnerCurrent(context.Owner) &&
               context.TargetGeneration == _settings.CurrentRemoteConfigurationTargetGeneration &&
               ReferenceEquals(context.Transport, Volatile.Read(ref _transport)) &&
               context.EndpointGeneration == configuration?.RemoteConfigurationEndpointGeneration &&
               string.Equals(context.Endpoint, GetConfigEndpoint(context.Transport, configuration), StringComparison.Ordinal);

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

        private readonly record struct PollContext(
            TracerManager? Owner,
            AgentTransport Transport,
            string Endpoint,
            long EndpointGeneration,
            long TargetGeneration);

        private sealed class AgentTransport(ExporterSettings exporter, IRemoteConfigurationApi api)
        {
            public ExporterSettings Exporter { get; } = exporter;

            public IRemoteConfigurationApi Api { get; } = api;
        }
    }
}
