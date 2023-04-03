// <copyright file="HotChocolateTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET5_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json.Linq;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class HotChocolate13Tests : HotChocolateTests
    {
        public HotChocolate13Tests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("HotChocolate", fixture, output, nameof(HotChocolate13Tests))
        {
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.HotChocolate), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task SubmitsTracesHttp(string packageVersion)
            => await RunSubmitsTraces(packageVersion);

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.HotChocolate), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task SubmitsTracesWebsockets(string packageVersion)
            => await RunSubmitsTraces(packageVersion, true);
    }

    [UsesVerify]
    public abstract class HotChocolateTests : TracingIntegrationTest, IClassFixture<AspNetCoreTestFixture>
    {
        private const string ServiceVersion = "1.0.0";

        private readonly string _testName;

        protected HotChocolateTests(string sampleAppName, AspNetCoreTestFixture fixture, ITestOutputHelper output, string testName)
            : base(sampleAppName, output)
        {
            SetServiceVersion(ServiceVersion);

            _testName = testName;

            Fixture = fixture;
            Fixture.SetOutput(output);
        }

        protected AspNetCoreTestFixture Fixture { get; }

        public override void Dispose()
        {
            Fixture.SetOutput(null);
        }

        public override Result ValidateIntegrationSpan(MockSpan span) =>
            span.Type switch
            {
                "graphql" => span.IsHotChocolate(),
                _ => Result.DefaultSuccess,
            };

        protected async Task RunSubmitsTraces(string packageVersion = "", bool usingWebsockets = false)
        {
            SetInstrumentationVerification();

            await Fixture.TryStartApp(this);
            var testStart = DateTime.UtcNow;
            var expectedSpans = await SubmitRequests(Fixture.HttpPort, usingWebsockets);

            var spans = Fixture.Agent.WaitForSpans(count: expectedSpans, minDateTime: testStart, returnAllOperations: true);
            foreach (var span in spans)
            {
                // TODO: Refactor to use ValidateIntegrationSpans when the HotChocolate server integration is fixed. It currently produces a service name of {service]-graphql
                var result = ValidateIntegrationSpan(span);
                Assert.True(result.Success, result.ToString());
            }

            var settings = VerifyHelper.GetSpanVerifierSettings();

            await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName($"HotChocolateTests{(usingWebsockets ? "Websockets" : string.Empty)}.SubmitsTraces")
                                  .DisableRequireUniquePrefix(); // all package versions should be the same

            VerifyInstrumentation(Fixture.Process);
        }

        private async Task<int> SubmitRequests(int aspNetCorePort, bool usingWebsockets)
        {
            var expectedGraphQlExecuteSpanCount = 0;
            var expectedAspNetcoreRequestSpanCount = 0;

            if (usingWebsockets)
            {
                await SubmitWebsocketRequests();
            }
            else
            {
                SubmitHttpRequests();
            }

            void SubmitHttpRequests()
            {
                // SUCCESS: query using GET
                SubmitGraphqlRequest(url: "/graphql?query=" + WebUtility.UrlEncode("query{book{title author{name}}}"), httpMethod: "GET", graphQlRequestBody: null);

                // SUCCESS: query using POST (default)
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""query Book{book{title author{name}}}""}");

                // SUCCESS: mutation
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""mutation m{addBook(book:{title:\""New Book\""}){book{title}}}""}");

                // FAILURE: query fails 'validate' step
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""{boook{title author{name}}}""}");

                // FAILURE: query fails 'execute' step
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""subscription NotImplementedSub{throwNotImplementedException{name}}""}");
            }

            async Task SubmitWebsocketRequests()
            {
                // SUCCESS: query using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""query Book{book{title author{name}}}"",""variables"": {}}}");

                // SUCCESS: mutation using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""mutation m{addBook(book:{title:\""New Book\""}){book{title}}}"",""variables"": {}}}");

                // FAILURE: query fails 'execute' step using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""subscription NotImplementedSub{throwNotImplementedException{name}}"",""variables"": {}}}");

                // FAILURE: query fails 'validate' step using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""{boook{title author{name}}}"",""variables"": {}}}");
            }

            return expectedGraphQlExecuteSpanCount + expectedAspNetcoreRequestSpanCount;

            void SubmitGraphqlRequest(
                string url,
                string httpMethod,
                string graphQlRequestBody)
            {
                expectedGraphQlExecuteSpanCount++;
                expectedAspNetcoreRequestSpanCount++;

                GraphQLTests.SubmitRequest(
                    Output,
                    aspNetCorePort,
                    new GraphQLTests.RequestInfo { Url = url, HttpMethod = httpMethod, RequestBody = graphQlRequestBody, });
            }

            async Task SubmitGraphqlWebsocketRequest(
                string url,
                string httpMethod,
                string graphQlRequestBody)
            {
                expectedGraphQlExecuteSpanCount++;
                expectedAspNetcoreRequestSpanCount++;

                await GraphQLTests.SubmitWebsocketRequest(
                    Output,
                    aspNetCorePort,
                    new GraphQLTests.RequestInfo() { Url = url, HttpMethod = httpMethod, RequestBody = graphQlRequestBody, });
            }
        }
    }
}

#endif
