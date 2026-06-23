// <copyright file="FlagEvaluationBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Benchmarks.Trace
{
    /// <summary>
    /// Hot-path cost of EVP flag-evaluation recording. Mirrors the work the OpenFeature
    /// <c>FlagEvalLoggingHook.FinallyAsync</c> charges the evaluating thread: a cheap bounded enqueue.
    /// Also measures the off-thread aggregation cost (enqueue + drain into the two-tier aggregator)
    /// so the design's "cheap capture, deferred aggregation" split is visible in ns/op + allocs.
    /// </summary>
    [MemoryDiagnoser]
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class FlagEvaluationBenchmark
    {
        private static readonly Dictionary<string, object?> Context = new()
        {
            { "user_tier", "premium" },
            { "region", "us-east-1" },
            { "beta_opt_in", true },
            { "request_count", 42 },
        };

        private FlagEvaluationApi _api = null!;

        [GlobalSetup]
        public void GlobalSetup()
        {
            // No-op transport: the benchmark exercises the capture + aggregation hot path, not the
            // network. The send loop is never started (we drive Enqueue/Drain directly).
            _api = new FlagEvaluationApi(new NoOpApiRequestFactory(), service: "bench", env: "ci", version: "1.0");
        }

        /// <summary>
        /// The cost the evaluation thread actually pays in the hook: a single cheap bounded enqueue.
        /// Aggregation runs later on the background worker, off this path.
        /// </summary>
        [Benchmark]
        public void EnqueueHotPath()
        {
            _api.EnqueueForTest(new FlagEvalEvent(
                flagKey: "checkout-redesign",
                variant: "treatment",
                allocationKey: "alloc-7",
                targetingKey: "user-123",
                evalTimeMs: 1_700_000_000_000L,
                contextAttrs: NewContext()));

            // Keep the queue bounded so the benchmark measures steady-state enqueue, not unbounded growth.
            _api.DrainQueueIntoAggregator();
        }

        /// <summary>
        /// The deferred aggregation cost (prune + canonical key + two-tier map insert) that the
        /// background worker pays per evaluation — the cost moved OFF the evaluation thread.
        /// </summary>
        [Benchmark]
        public long EnqueuePlusAggregate()
        {
            _api.EnqueueForTest(new FlagEvalEvent(
                flagKey: "checkout-redesign",
                variant: "treatment",
                allocationKey: "alloc-7",
                targetingKey: "user-123",
                evalTimeMs: 1_700_000_000_000L,
                contextAttrs: NewContext()));

            return _api.DrainQueueIntoAggregator();
        }

        private static Dictionary<string, object?> NewContext() => new(Context);

        /// <summary>A request factory whose transport is never invoked by this benchmark.</summary>
        private sealed class NoOpApiRequestFactory : IApiRequestFactory
        {
            public string Info(Uri endpoint) => endpoint?.ToString() ?? string.Empty;

            public Uri GetEndpoint(string relativePath) => new Uri("http://localhost/" + relativePath);

            public IApiRequest Create(Uri endpoint) => new NoOpApiRequest();

            public void SetProxy(WebProxy proxy, NetworkCredential credential)
            {
            }
        }

        private sealed class NoOpApiRequest : IApiRequest
        {
            public void AddHeader(string name, string value)
            {
            }

            public Task<IApiResponse> GetAsync() => throw new NotSupportedException();

            public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType) => throw new NotSupportedException();

            public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding) => throw new NotSupportedException();

            public Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression) => throw new NotSupportedException();

            public Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings) => throw new NotSupportedException();

            public Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary) => throw new NotSupportedException();

            public Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None) => throw new NotSupportedException();
        }
    }
}
