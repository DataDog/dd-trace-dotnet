// <copyright file="GraphQLTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
#if NETCOREAPP3_1_OR_GREATER
    public class GraphQL7SchemaV0Tests : GraphQL7Tests
    {
        public GraphQL7SchemaV0Tests(ITestOutputHelper output)
            : base(output, metadataSchemaVersion: "v0")
        {
        }
    }

    public class GraphQL7SchemaV1Tests : GraphQL7Tests
    {
        public GraphQL7SchemaV1Tests(ITestOutputHelper output)
            : base(output, metadataSchemaVersion: "v1")
        {
        }
    }

    public abstract class GraphQL7Tests : GraphQLTests
    {
        public GraphQL7Tests(ITestOutputHelper output, string metadataSchemaVersion)
            : base("GraphQL7", output, nameof(GraphQL7Tests), metadataSchemaVersion)
        {
        }

        // Can't currently run multi-api on Windows
        public static IEnumerable<object[]> TestData =>
            EnvironmentTools.IsWindows()
                ? new[] { new object[] { string.Empty } }
                : PackageVersions.GraphQL7;

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces(string packageVersion)
            => await RunSubmitsTraces(packageVersion: packageVersion);

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTracesWebsockets(string packageVersion)
            => await RunSubmitsTraces("SubmitsTracesWebsockets", packageVersion, true);
    }

    public class GraphQL4SchemaV0Tests : GraphQL4Tests
    {
        public GraphQL4SchemaV0Tests(ITestOutputHelper output)
            : base(output, metadataSchemaVersion: "v0")
        {
        }
    }

    public class GraphQL4SchemaV1Tests : GraphQL4Tests
    {
        public GraphQL4SchemaV1Tests(ITestOutputHelper output)
            : base(output, metadataSchemaVersion: "v1")
        {
        }
    }

    public abstract class GraphQL4Tests : GraphQLTests
    {
        public GraphQL4Tests(ITestOutputHelper output, string metadataSchemaVersion)
            : base("GraphQL4", output, nameof(GraphQL4Tests), metadataSchemaVersion)
        {
        }

        // Can't currently run multi-api on Windows
        public static IEnumerable<object[]> TestData =>
            EnvironmentTools.IsWindows()
                ? new[] { new object[] { string.Empty } }
                : PackageVersions.GraphQL;

        [SkippableTheory]
        [MemberData(nameof(TestData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces(string packageVersion)
            => await RunSubmitsTraces(packageVersion: packageVersion);
    }
#endif

    public class GraphQL3SchemaV0Tests : GraphQL3Tests
    {
        public GraphQL3SchemaV0Tests(ITestOutputHelper output)
            : base(output, metadataSchemaVersion: "v0")
        {
        }
    }

    public class GraphQL3SchemaV1Tests : GraphQL3Tests
    {
        public GraphQL3SchemaV1Tests(ITestOutputHelper output)
            : base(output, metadataSchemaVersion: "v1")
        {
        }
    }

    public abstract class GraphQL3Tests : GraphQLTests
    {
        public GraphQL3Tests(ITestOutputHelper output, string metadataSchemaVersion)
            : base("GraphQL3", output, nameof(GraphQL3Tests), metadataSchemaVersion)
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task SubmitsTraces()
            => await RunSubmitsTraces();
    }

    public class GraphQL2SchemaV0Tests : GraphQL2Tests
    {
        public GraphQL2SchemaV0Tests(ITestOutputHelper output)
            : base(output, metadataSchemaVersion: "v0")
        {
        }
    }

    public class GraphQL2SchemaV1Tests : GraphQL2Tests
    {
        public GraphQL2SchemaV1Tests(ITestOutputHelper output)
            : base(output, metadataSchemaVersion: "v1")
        {
        }
    }

    public abstract class GraphQL2Tests : GraphQLTests
    {
        public GraphQL2Tests(ITestOutputHelper output, string metadataSchemaVersion)
            : base("GraphQL", output, nameof(GraphQL2Tests), metadataSchemaVersion)
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task SubmitsTraces()
        {
            if (EnvironmentTools.IsWindows()
             && !EnvironmentHelper.IsCoreClr()
             && EnvironmentTools.IsTestTarget64BitProcess())
            {
                throw new SkipException(
                    "ASP.NET Core running on .NET Framework requires x86, because it uses " +
                    "the x86 version of libuv unless you compile the dll _explicitly_ for x64, " +
                    "which we don't do any more");
            }

            await RunSubmitsTraces();
        }
    }

    [UsesVerify]
    public abstract class GraphQLTests : TracingIntegrationTest
    {
        private const string ServiceVersion = "1.0.0";

        private readonly string _testName;
        private readonly string _metadataSchemaVersion;

        protected GraphQLTests(string sampleAppName, ITestOutputHelper output, string testName, string metadataSchemaVersion)
            : base(sampleAppName, output)
        {
            SetServiceVersion(ServiceVersion);
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);

            _testName = testName;
            _metadataSchemaVersion = metadataSchemaVersion;
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsGraphQL(metadataSchemaVersion);

        protected async Task RunSubmitsTraces(string testName = "SubmitsTraces", string packageVersion = "", bool usingWebsockets = false)
        {
            using var fixture = new AspNetCoreTestFixture();
            var isExternalSpan = _metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-graphql" : EnvironmentHelper.FullSampleName;

            SetInstrumentationVerification();

            await fixture.TryStartApp(this, packageVersion: packageVersion);
            var testStart = DateTime.UtcNow;
            var expectedSpans = await SubmitRequests(fixture.HttpPort, usingWebsockets);

            var spans = fixture.Agent.WaitForSpans(count: expectedSpans, minDateTime: testStart, returnAllOperations: true);

            var graphQLSpans = spans.Where(span => span.Type == "graphql");
            ValidateIntegrationSpans(graphQLSpans, _metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

            var settings = VerifyHelper.GetSpanVerifierSettings();

            // hacky scrubber for the fact that version 4.1.0+ switched to using " in error message in one place
            // where every other version uses '
            settings.AddSimpleScrubber("Did you mean \"appearsIn\"", "Did you mean 'appearsIn'");
            // Graphql 5 has different error message for missing subscription
            settings.AddSimpleScrubber("Could not resolve source stream for field", "Error trying to resolve field");

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            var fxSuffix = EnvironmentHelper.IsCoreClr() ? string.Empty : ".netfx";
            await VerifyHelper.VerifySpans(spans, settings)
                              .UseFileName($"{_testName}.{testName}{fxSuffix}.Schema{_metadataSchemaVersion.ToUpper()}")
                              .DisableRequireUniquePrefix(); // all package versions should be the same

            VerifyInstrumentation(fixture.Process);
        }

        private async Task<int> SubmitRequests(int aspNetCorePort, bool usingWebsockets)
        {
            var expectedGraphQlValidateSpanCount = 0;
            var expectedGraphQlExecuteSpanCount = 0;
            var expectedAspNetcoreRequestSpanCount = 0;

            if (usingWebsockets)
            {
                await SubmitWebsocketRequests();
            }
            else
            {
                var isV7 = _testName.Contains("7");
                SubmitHttpRequests(isV7);
            }

            Output.WriteLine($"[SPANS] Expected graphql.execute Spans: {expectedGraphQlExecuteSpanCount}");
            Output.WriteLine($"[SPANS] Expected graphql.validate Spans: {expectedGraphQlValidateSpanCount}");
            Output.WriteLine($"[SPANS] Expected aspnet_core.request Spans: {expectedAspNetcoreRequestSpanCount}");
            Output.WriteLine($"[SPANS] Total Spans number: {(expectedGraphQlExecuteSpanCount + expectedGraphQlValidateSpanCount + expectedAspNetcoreRequestSpanCount)}");
            return expectedGraphQlExecuteSpanCount + expectedGraphQlValidateSpanCount + expectedAspNetcoreRequestSpanCount;

            void SubmitHttpRequests(bool isV7)
            {
                // SUCCESS: query using GET
                SubmitGraphqlRequest(url: "/graphql?query=" + WebUtility.UrlEncode("query{hero{name appearsIn}}"), httpMethod: "GET", graphQlRequestBody: null);

                // SUCCESS: query using POST (default)
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""query HeroQuery{hero {name appearsIn}}"",""operationName"": ""HeroQuery""}");

                // SUCCESS: mutation
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""mutation AddBobaFett($human:HumanInput!){createHuman(human: $human){id name}}"",""variables"":{""human"":{""name"": ""Boba Fett""}}}");

                // SUCCESS: subscription or FAILURE: fails 'validate' (can't do POST for subscription on v7+)
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{ ""query"":""subscription HumanAddedSub{humanAdded{name}}""}", failsValidation: isV7);

                // TODO: When parse is implemented, add a test that fails 'parse' step

                // FAILURE: query fails 'validate' step
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""query HumanError{human(id:1){name apearsIn}}""}", failsValidation: true);

                // FAILURE: query fails 'execute' step but fails at 'validate' on v7
                SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""subscription NotImplementedSub{throwNotImplementedException{name}}""}", failsValidation: isV7);

                // TODO: When parse is implemented, add a test that fails 'resolve' step
            }

            async Task SubmitWebsocketRequests()
            {
                // SUCCESS: query using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""query HeroQuery{hero {name appearsIn}}"",""variables"": {},}}", false);

                // FAILURE: query fails 'execute' step using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""subscription NotImplementedSub{throwNotImplementedException {name}}"",""variables"": {},}}", false);

                // FAILURE: query fails 'validate' step using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""query HumanError{human(id:1){name apearsIn}}"",""variables"": {},}}", true);

                // SUCCESS: mutation using Websocket
                await SubmitGraphqlWebsocketRequest(url: "/graphql", httpMethod: null, graphQlRequestBody: @"{""type"": ""start"",""id"": ""1"",""payload"": {""query"": ""mutation AddBobaFett($human:HumanInput!){createHuman(human: $human){id name}}"",""variables"": {""human"":{""name"": ""Boba Fett""}},}}", false);
            }

            void SubmitGraphqlRequest(
                string url,
                string httpMethod,
                string graphQlRequestBody,
                bool failsValidation = false)
            {
                expectedGraphQlValidateSpanCount++;

                if (!failsValidation)
                {
                    expectedGraphQlExecuteSpanCount++;
                }

                if (EnvironmentHelper.IsCoreClr())
                {
                    expectedAspNetcoreRequestSpanCount++;
                }

                GraphQLCommon.SubmitRequest(
                    Output,
                    aspNetCorePort,
                    new GraphQLCommon.RequestInfo() { Url = url, HttpMethod = httpMethod, RequestBody = graphQlRequestBody, });
            }

            async Task SubmitGraphqlWebsocketRequest(
                string url,
                string httpMethod,
                string graphQlRequestBody,
                bool failsValidation = false)
            {
                expectedGraphQlValidateSpanCount++;

                if (!failsValidation)
                {
                    expectedGraphQlExecuteSpanCount++;
                }

                expectedAspNetcoreRequestSpanCount++;
                await GraphQLCommon.SubmitWebsocketRequest(
                    Output,
                    aspNetCorePort,
                    new GraphQLCommon.RequestInfo() { Url = url, HttpMethod = httpMethod, RequestBody = graphQlRequestBody, });
            }
        }
    }
}
