// <copyright file="StatsBufferTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers.Stats;
using FluentAssertions;
using MessagePack;
using Xunit;

namespace Datadog.Trace.Tests.Agent
{
    public class StatsBufferTests
    {
        private static readonly List<byte[]> EmptyPeerTags = [];

        [Fact]
        public void KeyEquality()
        {
            var key1 = CreateKey("resource1", "service1", "operation1", "type1", 1, false);
            var key2 = CreateKey("resource1", "service1", "operation1", "type1", 1, false);

            key1.Should().Be(key2);
        }

        [Theory]
        [CombinatorialData]
        public void Serialization(bool propagateProcessTags, bool setServiceName)
        {
            const long expectedDuration = 42;

            // For Tracer Settings, the non mutable one.
            var collection = new NameValueCollection
            {
                { ConfigurationKeys.PropagateProcessTags, propagateProcessTags.ToString() },
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);

            var settings = MutableSettings.CreateForTesting(
                new(source),
                new()
                {
                    { ConfigurationKeys.Environment, "Env" },
                    { ConfigurationKeys.ServiceVersion, "v99.99" },
                    { ConfigurationKeys.ServiceName, setServiceName ? "AServiceName" : null },
                });

            var payload = new ClientStatsPayload(settings)
            {
                HostName = "Hostname",
            };

            var buffer = new StatsBuffer(payload);

            var key1 = CreateKey("resource1", "service1", "operation1", "type1", 1, true);
            var key2 = CreateKey("resource2", "service2", "operation2", "type2", 2, false);
            var key3 = CreateKey("resource3", "service3", "operation3", "type3", 2, true);

            var statsBucket1 = new StatsBucket(key1, EmptyPeerTags) { Duration = 1, Errors = 11, Hits = 111, TopLevelHits = 10 };
            var statsBucket2 = new StatsBucket(key2, EmptyPeerTags) { Duration = 2, Errors = 22, Hits = 222, TopLevelHits = 20 };
            var statsBucket3 = new StatsBucket(key3, EmptyPeerTags) { Duration = 3, Errors = 0, Hits = 0, TopLevelHits = 0 };

            buffer.Buckets.Add(key1, statsBucket1);
            buffer.Buckets.Add(key2, statsBucket2);
            buffer.Buckets.Add(key3, statsBucket3);

            var stream = new MemoryStream();
            buffer.Serialize(stream, expectedDuration);
            var result = MessagePackSerializer.Deserialize<MockClientStatsPayload>(stream.ToArray());

            result.Hostname.Should().Be(payload.HostName);
            result.Env.Should().Be(payload.Details.Environment);
            result.Version.Should().Be(payload.Details.Version);
            result.Lang.Should().Be(TracerConstants.Language);
            result.TracerVersion.Should().Be(TracerConstants.AssemblyVersion);
            result.RuntimeId.Should().Be(Tracer.RuntimeId);
            result.Sequence.Should().Be(1);
            result.AgentAggregation.Should().BeNull();
            result.Service.Should().Be(payload.Details.DefaultServiceName);
            result.Stats.Should().HaveCount(1);

            if (propagateProcessTags)
            {
                result.ProcessTags.Should().Contain(setServiceName ? "svc.user" : "svc.auto");
                result.ProcessTags.Should().NotContain(setServiceName ? "svc.auto" : "svc.user");
            }
            else
            {
                result.ProcessTags.Should().BeNull();
            }

            var bucket = result.Stats[0];

            bucket.Start.Should().Be(buffer.Start);
            bucket.Duration.Should().Be(expectedDuration);
            bucket.AgentTimeShift.Should().Be(0);
            bucket.Stats.Should().HaveCount(2); // statsBucket3 isn't serialized because it has no hits

            AssertStatsGroup(bucket.Stats.Single(g => g.Name == key1.OperationName), key1, statsBucket1);
            AssertStatsGroup(bucket.Stats.Single(g => g.Name == key2.OperationName), key2, statsBucket2);
        }

        [Fact]
        public void Reset()
        {
            var buffer = new StatsBuffer(new ClientStatsPayload(MutableSettings.CreateForTesting(new(), [])));

            var key1 = CreateKey("resource1", "service1", "operation1", "type1", 1, false);
            var key2 = CreateKey("resource2", "service2", "operation2", "type2", 2, false);

            var statsBucket1 = new StatsBucket(key1, EmptyPeerTags) { Duration = 1, Errors = 11, Hits = 111, TopLevelHits = 10 };
            var statsBucket2 = new StatsBucket(key2, EmptyPeerTags) { Duration = 2, Errors = 0, Hits = 0, TopLevelHits = 0 };

            buffer.Buckets.Add(key1, statsBucket1);
            buffer.Buckets.Add(key2, statsBucket2);

            // First reset - key1 should be cleared, key2 should be removed (because it has no hits)
            buffer.Reset();

            buffer.Buckets.Should().HaveCount(1);

            var kvp = buffer.Buckets.Single();

            kvp.Key.Should().Be(key1);

            kvp.Value.Duration.Should().Be(0);
            kvp.Value.OkSummary.GetCount().Should().Be(0);
            kvp.Value.ErrorSummary.GetCount().Should().Be(0);
            kvp.Value.Errors.Should().Be(0);
            kvp.Value.Hits.Should().Be(0);
            kvp.Value.TopLevelHits.Should().Be(0);

            // Second reset - key1 should be removed
            buffer.Reset();

            buffer.Buckets.Should().BeEmpty();
        }

        [Fact]
        public void IncrementSequence()
        {
            var buffer = new StatsBuffer(new ClientStatsPayload(MutableSettings.CreateForTesting(new(), [])));

            var key = CreateKey("resource1", "service1", "operation1", "type1", 1, false);
            var statsBucket = new StatsBucket(key, EmptyPeerTags) { Duration = 1, Errors = 11, Hits = 111, TopLevelHits = 10 };

            buffer.Buckets.Add(key, statsBucket);

            var stream = new MemoryStream();
            buffer.Serialize(stream, 1);
            var result = MessagePackSerializer.Deserialize<MockClientStatsPayload>(stream.ToArray());

            result.Sequence.Should().Be(1);

            stream = new MemoryStream();
            buffer.Serialize(stream, 1);
            result = MessagePackSerializer.Deserialize<MockClientStatsPayload>(stream.ToArray());

            result.Sequence.Should().Be(2);
        }

        [Fact]
        public void KeyEquality_NewDimensions()
        {
            // Different SpanKind
            var key1 = CreateKey("r", "s", "o", "t", 0, false, spanKind: "client");
            var key2 = CreateKey("r", "s", "o", "t", 0, false, spanKind: "server");
            key1.Should().NotBe(key2);

            // Different IsTraceRoot
            var key3 = CreateKey("r", "s", "o", "t", 0, false, isTraceRoot: true);
            var key4 = CreateKey("r", "s", "o", "t", 0, false, isTraceRoot: false);
            key3.Should().NotBe(key4);

            // Different HttpMethod
            var key5 = CreateKey("r", "s", "o", "t", 0, false, httpMethod: "GET");
            var key6 = CreateKey("r", "s", "o", "t", 0, false, httpMethod: "POST");
            key5.Should().NotBe(key6);

            // Different HttpEndpoint
            var key7 = CreateKey("r", "s", "o", "t", 0, false, httpEndpoint: "/users/{id}");
            var key8 = CreateKey("r", "s", "o", "t", 0, false, httpEndpoint: "/orders/{id}");
            key7.Should().NotBe(key8);

            // Different GrpcStatusCode
            var key9 = CreateKey("r", "s", "o", "t", 0, false, grpcStatusCode: 0);
            var key10 = CreateKey("r", "s", "o", "t", 0, false, grpcStatusCode: 2);
            key9.Should().NotBe(key10);

            // Different ServiceSource
            var key11 = CreateKey("r", "s", "o", "t", 0, false, serviceSource: "integration");
            var key12 = CreateKey("r", "s", "o", "t", 0, false, serviceSource: "user");
            key11.Should().NotBe(key12);

            // Different PeerTagsHash
            var key13 = CreateKey("r", "s", "o", "t", 0, false, peerTagsHash: 1);
            var key14 = CreateKey("r", "s", "o", "t", 0, false, peerTagsHash: 2);
            key13.Should().NotBe(key14);
        }

        private static StatsAggregationKey CreateKey(
            string resource,
            string service,
            string operationName,
            string type,
            int httpStatusCode,
            bool isSyntheticsRequest,
            string spanKind = null,
            bool isTraceRoot = false,
            string httpMethod = null,
            string httpEndpoint = null,
            int grpcStatusCode = 0,
            string serviceSource = null,
            ulong peerTagsHash = 0)
        {
            return new StatsAggregationKey(
                resource,
                service,
                operationName,
                type,
                httpStatusCode,
                isSyntheticsRequest,
                spanKind ?? string.Empty,
                isError: false,
                isTopLevel: false,
                isTraceRoot,
                httpMethod ?? string.Empty,
                httpEndpoint ?? string.Empty,
                grpcStatusCode,
                serviceSource ?? string.Empty,
                peerTagsHash);
        }

        private static void AssertStatsGroup(MockClientGroupedStats group, StatsAggregationKey expectedKey, StatsBucket expectedBucket)
        {
            group.Service.Should().Be(expectedKey.Service);
            group.Name.Should().Be(expectedKey.OperationName);
            group.Resource.Should().Be(expectedKey.Resource);
            group.HttpStatusCode.Should().Be(expectedKey.HttpStatusCode);
            group.Type.Should().Be(expectedKey.Type);
            group.DbType.Should().BeNull();
            // Hits/Errors/TopLevelHits use stochastic rounding from double to long.
            // Test values are whole numbers, so stochastic rounding == truncation.
            group.Hits.Should().Be((long)expectedBucket.Hits);
            group.Errors.Should().Be((long)expectedBucket.Errors);
            group.Duration.Should().Be(expectedBucket.Duration);
            group.Synthetics.Should().Be(expectedKey.IsSyntheticsRequest);
            group.TopLevelHits.Should().Be((long)expectedBucket.TopLevelHits);
            group.SpanKind.Should().Be(expectedKey.SpanKind);
            // Trilean: NOT_SET=0, TRUE=1, FALSE=2
            group.IsTraceRoot.Should().Be(expectedKey.IsTraceRoot ? 1 : 2);
            group.HttpMethod.Should().Be(expectedKey.HttpMethod);
            group.HttpEndpoint.Should().Be(expectedKey.HttpEndpoint);
            group.GrpcStatusCode.Should().Be(expectedKey.GrpcStatusCode);
            group.ServiceSource.Should().Be(expectedKey.ServiceSource);

            var stream = new MemoryStream();
            expectedBucket.ErrorSummary.Serialize(stream);
            group.ErrorSummary.Should().Equal(stream.ToArray());

            stream = new MemoryStream();
            expectedBucket.OkSummary.Serialize(stream);
            group.OkSummary.Should().Equal(stream.ToArray());
        }
    }
}
