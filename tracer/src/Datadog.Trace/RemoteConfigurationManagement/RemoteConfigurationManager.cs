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
            UpdateRcmClientTracer(_settings.InitialMutableSettings);
            _settingSubscription = _settings.SubscribeToChanges(changes =>
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

                var exporter = changes.EffectiveExporter;
                if (!ReferenceEquals(exporter, Volatile.Read(ref _transport).Exporter))
                {
                    Volatile.Write(ref _transport, CreateTransport(exporter));
                }
            });

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

        internal bool HasNativeWorkerToDrain
        {
            get
            {
#if SHARED_REMOTE_CONFIG
                if (_nativeLibraryPath is not null)
                {
                    lock (_workerGate)
                    {
                        return _worker is not null;
                    }
                }
#endif

                return false;
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

        public void Start()
        {
            lock (_workerGate)
            {
                if (_processExit.Task.IsCompleted || _worker is not null)
                {
                    return;
                }

                _worker = SchedulePolling();
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
                var transport = Volatile.Read(ref _transport);
                if (GetConfigEndpoint(transport, _discoveryService.CurrentConfiguration) is { Length: > 0 } endpoint && _subscriptionManager.HasAnySubscription)
                {
                    try
                    {
#if SHARED_REMOTE_CONFIG
                        if (nativePoller is not null)
                        {
                            await PollNative(nativePoller, transport, endpoint).ConfigureAwait(false);
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

            var rcm = CapturePollIdentity();
            var poll = _managedPoll ??= _subscriptionManager.CreateManagedPoller();
            return poll(rcm, request => transport.Api.GetConfigs(request, endpoint, cancellationToken), AcceptResponse, CommitResponse);

            bool AcceptResponse() => CanAcceptResponse() && IsCurrentTransport(transport, endpoint, _discoveryService.CurrentConfiguration);

            bool CanAcceptResponse()
                => !_processExit.Task.IsCompleted &&
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

                void CommitIfCurrent() => TryCommitResponse(transport, endpoint, TryCommit);

                bool TryCommit()
                {
                    if (!CanAcceptResponse())
                    {
                        return false;
                    }

                    update();
                    return true;
                }
            }
        }

#if SHARED_REMOTE_CONFIG
        private Task PollNative(NativeAgentPoller poller, AgentTransport transport, string endpoint)
        {
            var tracer = CapturePollIdentity();
            return poller.Poll(transport.Api, endpoint, tracer, _processExit.Task, IsCurrent, TryCommit);

            bool IsCurrent() => tracer.TargetGeneration == Volatile.Read(ref _rcmTracer).TargetGeneration &&
                                IsCurrentTransport(transport, endpoint, _discoveryService.CurrentConfiguration);

            bool TryCommit(Func<bool> publish) => TryCommitResponse(transport, endpoint, publish);
        }
#endif

        private RcmClientTracer CapturePollIdentity()
        {
            var tracer = Volatile.Read(ref _rcmTracer);
            EnrichTagsWithGitMetadata(tracer);
            tracer.ExtraServices = ExtraServicesProvider.Instance.GetExtraServices();
            return tracer;
        }

        private bool TryCommitResponse(AgentTransport transport, string endpoint, Func<bool> publish)
            => _settings.TryRunWithCurrentExporter(
                transport.Exporter,
                () => _discoveryService.TryRunWithCurrentConfiguration(configuration => IsCurrentTransport(transport, endpoint, configuration) && publish()));

        private string? GetConfigEndpoint(AgentTransport transport, AgentConfiguration? configuration)
            => configuration is not null &&
               ReferenceEquals(configuration.DiscoverySettings, transport.Exporter) &&
               ReferenceEquals(transport.Exporter, _settings.CurrentExporterSettings)
                   ? configuration.ConfigurationEndpoint
                   : null;

        private bool IsCurrentTransport(AgentTransport transport, string endpoint, AgentConfiguration? configuration)
            => ReferenceEquals(transport, Volatile.Read(ref _transport)) &&
               string.Equals(endpoint, GetConfigEndpoint(transport, configuration), StringComparison.Ordinal);

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

        private sealed class AgentTransport(ExporterSettings exporter, IRemoteConfigurationApi api)
        {
            public ExporterSettings Exporter { get; } = exporter;

            public IRemoteConfigurationApi Api { get; } = api;
        }
    }
}
