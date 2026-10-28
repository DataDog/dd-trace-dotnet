// <copyright file="RequestInvokerHandlerSendAsyncIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.CosmosDb
{
    public class RequestInvokerHandlerSendAsyncIntegrationTests
    {
        public static TheoryData<bool, int, string> ContextScenarios => new()
        {
            { false, 0, "gateway" },
            { false, 1, "direct" },
            { false, 2, "other" },
            // Disposal must not prevent reading metadata directly from the fields.
            { true, 0, "gateway" },
        };

        [Theory]
        [MemberData(nameof(ContextScenarios))]
        public void OnMethodBegin_CreatesSpanWhenContextIsDisposed(bool disposed, int connectionMode, string expectedMode)
        {
            var context = new ClientContext(disposed, connectionMode);
            var handler = new RequestInvokerHandler(new Client(context)).DuckCast<IRequestInvokerHandlerProxy>();
            var container = new { Id = "myColl", Database = new { Id = "myDb" } }.DuckCast<IContainer>();
            var state = RequestInvokerHandlerSendAsyncIntegration.OnMethodBegin<IRequestInvokerHandlerProxy, IContainer>(
                handler, "dbs/myDb/colls/myColl/docs/item1", "Document", "Read", null, container, null, null, null, null, default);

            using var scope = Assert.IsType<Scope>(state.Scope);
            scope.Span.ResourceName.Should().Be("Read dbs/myDb/colls/myColl/docs/?");
            scope.Span.Type.Should().Be(SpanTypes.Sql);
            var tags = scope.Span.Tags.Should().BeAssignableTo<CosmosDbTags>().Subject;
            tags.UserAgent.Should().Be("cosmos-test-agent");
            tags.ConnectionMode.Should().Be(expectedMode);
            tags.ContainerId.Should().Be("myColl");
            tags.DatabaseId.Should().Be("myDb");
            var expectedTags = Tracer.Instance.CurrentTraceSettings.Schema.Database.CreateCosmosDbTags();
            expectedTags.SetEndpoint(new Uri("https://localhost:8081"));
            tags.Host.Should().Be(expectedTags.Host);
            context.PropertyReads.Should().Be(0);
        }

        [Theory]
        [InlineData("dbs/myDb", "dbs/myDb")]
        [InlineData("dbs/myDb/colls/myColl/docs", "dbs/myDb/colls/myColl/docs")]
        [InlineData("dbs/myDb/colls/myColl/docs/item1", "dbs/myDb/colls/myColl/docs/?")]
        [InlineData("dbs/myDb/clientencryptionkeys/key1", "dbs/myDb/clientencryptionkeys/?")]
        [InlineData("dbs/myDb/users/user1/permissions/perm1", "dbs/myDb/users/?/permissions/?")]
        public void NormalizeResourceUri_RedactsIdsKeepingDbAndContainerNames(string input, string expected)
        {
            var result = RequestInvokerHandlerSendAsyncIntegration.NormalizeResourceUri(input);

            result.Should().Be(expected);
        }

        private class RequestInvokerHandler
        {
            // Matches the SDK field accessed through duck typing.
            private readonly Client client;

            public RequestInvokerHandler(Client client) => this.client = client;
        }

        private class Client
        {
            public Client(ClientContext context) => ClientContext = context;

            public Uri Endpoint => new("https://localhost:8081");

            public ClientContext ClientContext { get; }
        }

        private class ClientContext
        {
            private readonly bool _disposed;
            // Match the SDK fields accessed through duck typing.
            private readonly ClientOptions clientOptions;
            private readonly string userAgent = "cosmos-test-agent";

            public ClientContext(bool disposed, int connectionMode)
            {
                _disposed = disposed;
                clientOptions = new ClientOptions { ConnectionMode = connectionMode };
            }

            public int PropertyReads { get; private set; }

            public Client Client => ThrowIfDisposed(new Client(this));

            public string UserAgent => ThrowIfDisposed(userAgent);

            public ClientOptions ClientOptions => ThrowIfDisposed(clientOptions);

            private T ThrowIfDisposed<T>(T value)
            {
                PropertyReads++;
                return _disposed ? throw new ObjectDisposedException(nameof(ClientContext)) : value;
            }
        }

        private class ClientOptions
        {
            public int ConnectionMode { get; set; }
        }
    }
}
