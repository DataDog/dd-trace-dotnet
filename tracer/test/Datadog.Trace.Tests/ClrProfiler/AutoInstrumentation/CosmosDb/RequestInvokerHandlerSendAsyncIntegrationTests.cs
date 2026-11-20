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
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.CosmosDb
{
    public class RequestInvokerHandlerSendAsyncIntegrationTests
    {
        public interface IClientContext
        {
            object Client { get; }

            string UserAgent { get; }

            object ClientOptions { get; }
        }

        [Fact]
        public void OnMethodBegin_CreatesSpanWhenContextIsDisposed()
        {
            var context = new Mock<IClientContext>(MockBehavior.Strict);
            context.SetupGet(c => c.Client).Throws(new ObjectDisposedException("CosmosClient"));
            var client = new { Endpoint = new Uri("https://localhost:8081"), ClientContext = context.Object };
            var handler = new RequestInvokerHandler(client).DuckCast<IRequestInvokerHandlerProxy>();
            var container = new { Id = "myColl", Database = new { Id = "myDb" } }.DuckCast<IContainer>();
            var state = RequestInvokerHandlerSendAsyncIntegration.OnMethodBegin<IRequestInvokerHandlerProxy, IContainer>(
                handler, "dbs/myDb/colls/myColl/docs/item1", "Document", "Read", null, container, null, null, null, null, default);

            using var scope = Assert.IsType<Scope>(state.Scope);
            scope.Span.ResourceName.Should().Be("Read dbs/myDb/colls/myColl/docs/?");
            var tags = scope.Span.Tags.Should().BeAssignableTo<CosmosDbTags>().Subject;
            tags.UserAgent.Should().BeNull();
            tags.ConnectionMode.Should().BeNull();
            context.VerifyGet(c => c.Client, Times.Once);
            context.VerifyNoOtherCalls();
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
            private readonly object client;

            public RequestInvokerHandler(object client) => this.client = client;
        }
    }
}
