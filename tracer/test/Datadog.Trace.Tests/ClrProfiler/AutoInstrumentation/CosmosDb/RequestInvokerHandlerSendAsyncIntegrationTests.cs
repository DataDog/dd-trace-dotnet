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
using Microsoft.Azure.Cosmos;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.CosmosDb
{
    public class RequestInvokerHandlerSendAsyncIntegrationTests
    {
        [Fact]
        public void OnMethodBegin_CreatesSpanAfterRealClientIsDisposed()
        {
            // Constructing and disposing the SDK client requires no Cosmos DB server.
            using var client = new CosmosClient(
                "https://localhost:8081",
                Convert.ToBase64String(new byte[64]),
                new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway });
            var context = client.DuckCast<CosmosClientStruct>().ClientContext;
            var container = client.GetContainer("myDb", "myColl").DuckCast<IContainer>();
            var expectedUserAgent = context.DuckCast<CosmosContextClientStruct>().UserAgent;
            expectedUserAgent.Should().NotBeNullOrEmpty();

            client.Dispose();

            // Verify that the real SDK now rejects the property-based metadata access.
            Assert.Throws<ObjectDisposedException>(() => context.DuckCast<CosmosContextClientStruct>());

            var handler = new SdkClientRequestInvokerHandler(client).DuckCast<IRequestInvokerHandlerProxy>();
            var state = RequestInvokerHandlerSendAsyncIntegration.OnMethodBegin<IRequestInvokerHandlerProxy, IContainer>(
                handler, "dbs/myDb/colls/myColl/docs/item1", "Document", "Read", null, container, null, null, null, null, default);

            using var scope = Assert.IsType<Scope>(state.Scope);
            scope.Span.ResourceName.Should().Be("Read dbs/myDb/colls/myColl/docs/?");
            var tags = scope.Span.Tags.Should().BeAssignableTo<CosmosDbTags>().Subject;
            tags.UserAgent.Should().Be(expectedUserAgent);
            tags.ConnectionMode.Should().Be("gateway");
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

        private class SdkClientRequestInvokerHandler
        {
            private readonly CosmosClient client;

            public SdkClientRequestInvokerHandler(CosmosClient client) => this.client = client;
        }
    }
}
