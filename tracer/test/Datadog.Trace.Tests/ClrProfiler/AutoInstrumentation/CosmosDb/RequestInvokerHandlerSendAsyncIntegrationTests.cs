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
        [Theory]
        [InlineData(false, 0, "gateway")]
        [InlineData(false, 1, "direct")]
        [InlineData(false, 2, "other")]
        [InlineData(true, 0, "gateway")]
        [InlineData(true, 1, "direct")]
        [InlineData(true, 2, "other")]
        public void OnMethodBegin_CreatesSpanWhenContextIsDisposed(bool disposed, int connectionMode, string expectedMode)
        {
            var context = new ClientContext(disposed, connectionMode);
            var handler = new RequestInvokerHandler(new Client(context)).DuckCast<IRequestInvokerHandlerProxy>();
            var state = RequestInvokerHandlerSendAsyncIntegration.OnMethodBegin<IRequestInvokerHandlerProxy, IContainer>(
                handler, "dbs/myDb/colls/myColl/docs/item1", "Document", "Read", null, null!, null, null, null, null, default);

            using var scope = state.Scope;
            scope.Should().NotBeNull();
            scope!.Span.ResourceName.Should().Be("Read dbs/myDb/colls/myColl/docs/?");
            scope.Span.Type.Should().Be(SpanTypes.Sql);
            var tags = scope.Span.Tags.Should().BeAssignableTo<CosmosDbTags>().Subject;
            tags.UserAgent.Should().Be(disposed ? null : "cosmos-test-agent");
            tags.ConnectionMode.Should().Be(disposed ? null : expectedMode);
            var expectedTags = Tracer.Instance.CurrentTraceSettings.Schema.Database.CreateCosmosDbTags();
            expectedTags.SetEndpoint(new Uri("https://localhost:8081"));
            tags.Host.Should().Be(expectedTags.Host);
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
            private readonly ClientOptions _clientOptions;

            public ClientContext(bool disposed, int connectionMode)
            {
                _disposed = disposed;
                _clientOptions = new ClientOptions { ConnectionMode = connectionMode };
            }

            public Client Client => ThrowIfDisposed(new Client(this));

            public string UserAgent => ThrowIfDisposed("cosmos-test-agent");

            public ClientOptions ClientOptions => ThrowIfDisposed(_clientOptions);

            private T ThrowIfDisposed<T>(T value) => _disposed ? throw new ObjectDisposedException(nameof(ClientContext)) : value;
        }

        private class ClientOptions
        {
            public int ConnectionMode { get; set; }
        }
    }
}
