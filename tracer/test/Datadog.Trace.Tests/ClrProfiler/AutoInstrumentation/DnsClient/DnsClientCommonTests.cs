// <copyright file="DnsClientCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.DnsClient;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.DnsClient
{
    public class DnsClientCommonTests
    {
        private const string FirstServer = "192.0.2.1";
        private const string AnsweringServer = "192.0.2.2";

        [Theory]
        [CombinatorialData]
        public async Task MapsAnsweringServerOnCompletion(bool isAsync, bool mapFirstServer, bool mapAnsweringServer, bool hasDnsError)
        {
            var mappings = new List<string>();
            if (mapFirstServer)
            {
                mappings.Add($"{FirstServer}:first-resolver");
            }

            if (mapAnsweringServer)
            {
                mappings.Add($"{AnsweringServer}:answering-resolver");
            }

            var settings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.MetadataSchemaVersion, "v1" },
                { ConfigurationKeys.PeerServiceNameMappings, string.Join(",", mappings) },
            });
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            using var scope = CreateScope(tracer);
            var response = CreateResponse(hasDnsError).DuckCast<IDnsQueryResponse>();

            Complete(isAsync, scope, response, exception: null).Should().BeSameAs(response);

            scope.Span.GetTag(Tags.OutHost).Should().Be(AnsweringServer);
            scope.Span.GetTag(Tags.NetworkDestinationPort).Should().Be("5353");
            scope.Span.GetTag(Tags.PeerService).Should().Be(mapAnsweringServer ? "answering-resolver" : AnsweringServer);
            scope.Span.GetTag(Tags.PeerServiceRemappedFrom).Should().Be(mapAnsweringServer ? AnsweringServer : null);
            scope.Span.GetTag(Tags.PeerServiceSource).Should().Be(mapAnsweringServer ? Tags.PeerService : Tags.OutHost);
            scope.Span.Error.Should().Be(hasDnsError);
            scope.Span.IsFinished.Should().BeTrue();
            tracer.InternalActiveScope.Should().BeNull();
        }

        [Theory]
        [CombinatorialData]
        public async Task MapsCandidateServerWhenNoResponseIsAvailable(bool isAsync, bool throwsException)
        {
            var settings = TracerSettings.Create(new()
            {
                { ConfigurationKeys.MetadataSchemaVersion, "v1" },
                { ConfigurationKeys.PeerServiceNameMappings, $"{FirstServer}:first-resolver" },
            });
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            using var scope = CreateScope(tracer);
            var response = DuckType.GetOrCreateProxyType(typeof(IDnsQueryResponse), CreateResponse(hasDnsError: false).GetType())
                                   .CreateInstance<IDnsQueryResponse>(null);
            var exception = throwsException ? new TimeoutException("DNS query timed out") : null;

            Complete(isAsync, scope, response, exception);

            scope.Span.GetTag(Tags.OutHost).Should().Be(FirstServer);
            scope.Span.GetTag(Tags.PeerService).Should().Be("first-resolver");
            scope.Span.GetTag(Tags.PeerServiceRemappedFrom).Should().Be(FirstServer);
            scope.Span.Error.Should().Be(throwsException);
            scope.Span.IsFinished.Should().BeTrue();
            tracer.InternalActiveScope.Should().BeNull();
        }

        private static Scope CreateScope(Tracer tracer)
        {
            var question = new { QueryName = "example.org", QuestionType = "A", QuestionClass = "IN" }.DuckCast<IDnsQuestion>();
            var servers = new[] { new { Address = FirstServer, Port = 53 }, new { Address = AnsweringServer, Port = 5353 } };
            var scope = DnsClientCommon.CreateScope(tracer, question, servers);
            scope.Should().NotBeNull();
            return scope!;
        }

        private static object CreateResponse(bool hasDnsError) => new
        {
            Header = new { ResponseCode = hasDnsError ? 3 : 0 },
            Answers = Array.Empty<object>(),
            NameServer = new { Address = AnsweringServer, Port = 5353 },
        };

        private static IDnsQueryResponse? Complete(bool isAsync, Scope scope, IDnsQueryResponse response, Exception? exception)
        {
            var state = new CallTargetState(scope);
            return isAsync
                       ? QueryInternalAsyncIntegration.OnAsyncMethodEnd<object, IDnsQueryResponse>(null!, response, exception, in state)
                       : QueryInternalIntegration.OnMethodEnd<object, IDnsQueryResponse>(null!, response, exception, in state).GetReturnValue();
        }
    }
}
