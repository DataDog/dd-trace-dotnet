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
    /// <c>FlagEvalEVPHook.FinallyAsync</c> charges the evaluating thread: a cheap bounded enqueue.
    /// Also measures the off-thread aggregation cost (enqueue + drain into the two-tier aggregator)
    /// so the design's "cheap capture, deferred aggregation" split is visible in ns/op + allocs.
    /// </summary>
    [MemoryDiagnoser]
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class FlagEvaluationBenchmark
    {
        [Params(
            "typical/100flags_50users_10fields",
            "stress/10flags_1000users_250fields",
            "scale/2500flags_500users_20fields")]
        public string Profile { get; set; } = "typical/100flags_50users_10fields";

        private FlagEvaluationApi _api = null!;
        private Dictionary<string, object?> _context = null!;
        private string[] _flagKeys = null!;
        private string[] _targetingKeys = null!;
        private int _counter;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var profile = BenchmarkProfile.FromName(Profile);
            _context = NewContext(profile.NumFields);
            _flagKeys = NewKeys("bench-flag-", profile.NumFlags);
            _targetingKeys = NewKeys("bench-user-", profile.NumUsers);
            _counter = 0;

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
            _api.EnqueueForTest(NextEvent());

            // Keep the queue bounded so the benchmark measures steady-state enqueue, not unbounded growth.
            _api.DrainQueueIntoAggregator();
        }

        /// <summary>
        /// The deferred aggregation cost (prune + canonical key + two-tier map insert) that the
        /// background worker pays per evaluation - the cost moved OFF the evaluation thread.
        /// </summary>
        [Benchmark]
        public long EnqueuePlusAggregate()
        {
            _api.EnqueueForTest(NextEvent());

            return _api.DrainQueueIntoAggregator();
        }

        private FlagEvalEvent NextEvent()
        {
            var i = _counter++;
            return new FlagEvalEvent(
                flagKey: _flagKeys[i % _flagKeys.Length],
                variant: "variant-" + (i % 4),
                allocationKey: "alloc-" + (i % _flagKeys.Length),
                targetingKey: _targetingKeys[i % _targetingKeys.Length],
                evalTimeMs: 1_700_000_000_000L + i,
                contextAttrs: new Dictionary<string, object?>(_context));
        }

        private static Dictionary<string, object?> NewContext(int fields)
        {
            var context = new Dictionary<string, object?>();
            for (var i = 0; i < fields; i++)
            {
                context["field" + i] = "value";
            }

            return context;
        }

        private static string[] NewKeys(string prefix, int count)
        {
            var keys = new string[count];
            for (var i = 0; i < count; i++)
            {
                keys[i] = prefix + i;
            }

            return keys;
        }

        private readonly struct BenchmarkProfile
        {
            public BenchmarkProfile(int numFlags, int numUsers, int numFields)
            {
                NumFlags = numFlags;
                NumUsers = numUsers;
                NumFields = numFields;
            }

            public int NumFlags { get; }

            public int NumUsers { get; }

            public int NumFields { get; }

            public static BenchmarkProfile FromName(string name)
            {
                return name switch
                {
                    "typical/100flags_50users_10fields" => new BenchmarkProfile(100, 50, 10),
                    "stress/10flags_1000users_250fields" => new BenchmarkProfile(10, 1_000, 250),
                    "scale/2500flags_500users_20fields" => new BenchmarkProfile(2_500, 500, 20),
                    _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown benchmark profile")
                };
            }
        }

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
