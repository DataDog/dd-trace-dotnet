// <copyright file="GetRecordsIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class GetRecordsIntegrationTests
    {
        private const string StreamArn = "arn:aws:kinesis:us-east-1:123456789012:stream/my-stream";

        [Fact]
        public async Task SyncGetRecords_SetsConsumeCheckpoint()
        {
            await using var tracer = CreateTracerWithDsm();

            var pathway = RunSyncGetRecords();

            pathway.Should().NotBeNull("sync GetRecords should set a DSM consume checkpoint when records are returned");
        }

        [Fact]
        public async Task SyncGetRecords_ProducesSamePathwayAsAsyncGetRecords()
        {
            await using var tracer = CreateTracerWithDsm();

            var asyncPathway = RunAsyncGetRecords();
            var syncPathway = RunSyncGetRecords();

            asyncPathway.Should().NotBeNull();
            syncPathway.Should().NotBeNull();
            syncPathway!.Value.Hash.Should().Be(asyncPathway!.Value.Hash);
        }

        private static ScopedTracer CreateTracerWithDsm()
        {
            var settings = TracerSettings.Create(
                new()
                {
                    { ConfigurationKeys.DataStreamsMonitoring.Enabled, "true" },
                    { ConfigurationKeys.Environment, "foo" },
                    { ConfigurationKeys.ServiceName, "bar" },
                    { ConfigurationKeys.PropagateProcessTags, "false" },
                });
            var tracer = TracerHelper.Create(settings, scopeManager: new AsyncLocalScopeManager());
            TracerRestorerAttribute.SetTracer(tracer);
            tracer.TracerManager.DataStreamsManager.IsEnabled.Should().BeTrue();
            return tracer;
        }

        private static PathwayContext? RunSyncGetRecords()
        {
            var request = new FakeGetRecordsRequest { StreamARN = StreamArn }.DuckCast<IGetRecordsRequest>()!;
            var response = CreateResponse();

            // CallTarget passes the generated duck proxy type as the generic argument, which satisfies both IGetRecordsRequest and IDuckType.
            var begin = typeof(GetRecordsIntegration)
                       .GetMethod("OnMethodBegin", BindingFlags.Static | BindingFlags.NonPublic)!
                       .MakeGenericMethod(typeof(object), request.GetType());
            var state = (CallTargetState)begin.Invoke(null, [new object(), request])!;
            var span = state.Scope!.Span;

            GetRecordsIntegration.OnMethodEnd<object, IGetRecordsResponse>(new object(), response, null, in state);

            return span.Context.PathwayContext;
        }

        private static PathwayContext? RunAsyncGetRecords()
        {
            var request = new FakeGetRecordsRequest { StreamARN = StreamArn }.DuckCast<IGetRecordsRequest>()!;
            var response = CreateResponse();

            var begin = typeof(GetRecordsAsyncIntegration)
                       .GetMethod("OnMethodBegin", BindingFlags.Static | BindingFlags.NonPublic)!
                       .MakeGenericMethod(typeof(object), request.GetType());
            var state = (CallTargetState)begin.Invoke(null, [new object(), request, CancellationToken.None])!;
            var span = state.Scope!.Span;

            GetRecordsAsyncIntegration.OnAsyncMethodEnd<object, IGetRecordsResponse>(new object(), response, null, in state);

            return span.Context.PathwayContext;
        }

        private static IGetRecordsResponse CreateResponse()
            => new FakeGetRecordsResponse
            {
                Records = [new FakeRecord { Data = new MemoryStream([1, 2, 3]) }],
            }.DuckCast<IGetRecordsResponse>()!;

        public class FakeGetRecordsRequest
        {
            public string? StreamARN { get; set; }
        }

        public class FakeGetRecordsResponse
        {
            public List<FakeRecord> Records { get; set; } = [];
        }

        public class FakeRecord
        {
            public MemoryStream? Data { get; set; }
        }
    }
}
