// <copyright file="NativeStatsAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Agent.Native
{
    internal class NativeStatsAggregator : IStatsAggregator
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<NativeStatsAggregator>();

        private readonly TaskCompletionSource<bool> _processExit;
        private readonly TimeSpan _bucketDuration;
        private readonly Task _flushTask;

        private readonly StatsDiscovery _statsDiscovery;

        internal NativeStatsAggregator(ImmutableTracerSettings settings, IDiscoveryService discoveryService)
        {
            _statsDiscovery = new StatsDiscovery(discoveryService);
            _processExit = new TaskCompletionSource<bool>();
            _bucketDuration = TimeSpan.FromSeconds(settings.StatsComputationInterval);

            _flushTask = Task.Run(Flush);
            _flushTask.ContinueWith(t => Log.Error(t.Exception, "Error in StatsAggregator"), TaskContinuationOptions.OnlyOnFaulted);

            try
            {
                ExporterBindings.CreateStatsExporter(
                    HostMetadata.Instance.Hostname,
                    settings.EnvironmentInternal,
                    settings.ServiceVersionInternal,
                    TracerConstants.Language,
                    TracerConstants.AssemblyVersion,
                    Tracer.RuntimeId,
                    settings.ServiceNameInternal,
                    ContainerMetadata.GetContainerId(),
                    settings.GitCommitSha,
                    null,
                    settings.ExporterInternal.AgentUriInternal.ToString());
            }
            catch (Exception e)
            {
                Log.Error(e, "An error happened while configuring transport");
                return;
            }
        }

        public bool? CanComputeStats => _statsDiscovery.CanComputeStats;

        public ArraySegment<Span> ProcessTrace(ArraySegment<Span> trace)
        {
            // processing happens on native side
            return trace;
        }

        public Task DisposeAsync()
        {
            _statsDiscovery.Dispose();
            _processExit.TrySetResult(true);
            return _flushTask;
        }

        public void Add(params Span[] spans)
        {
            AddRange(new ArraySegment<Span>(spans, 0, spans.Length));
        }

        public void AddRange(ArraySegment<Span> spans)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                var span = spans.Array[i + spans.Offset];

                if ((!span.IsTopLevel && span.GetMetric(Tags.Measured) != 1.0) || span.GetMetric(Tags.PartialSnapshot) > 0)
                {
                    continue;
                }

                var rawHttpStatusCode = span.GetTag(Tags.HttpStatusCode);

                if (rawHttpStatusCode == null || !int.TryParse(rawHttpStatusCode, out var httpStatusCode))
                {
                    httpStatusCode = 0;
                }

                ExporterBindings.AddSpanToBucket(
                    span.ResourceName,
                    span.ServiceName,
                    span.OperationName,
                    span.Type,
                    httpStatusCode,
                    span.Context.Origin == "synthetics",
                    span.IsTopLevel,
                    span.Error,
                    span.Duration.ToNanoseconds());
            }
        }

        internal async Task Flush()
        {
            // Use a do/while loop to still flush once if _processExit is already completed (this makes testing easier)
            do
            {
                if (CanComputeStats == false)
                {
                    // TODO: When we implement the feature to continuously poll the Agent Configuration,
                    // we may want to stay in this loop instead of returning
                    return;
                }

                await Task.WhenAny(_processExit.Task, Task.Delay(_bucketDuration)).ConfigureAwait(false);

                // Push the metrics
                if (CanComputeStats == true)
                {
                    ExporterBindings.FlushStats(_bucketDuration.ToNanoseconds());
                }
            }
            while (!_processExit.Task.IsCompleted);
        }
    }
}
