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
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
#if NETCOREAPP3_1_OR_GREATER
    public class GraphQL4Tests : GraphQLTests
    {
        public GraphQL4Tests(ITestOutputHelper output)
            : base("GraphQL4", output, nameof(GraphQL4Tests))
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
            => await RunSubmitsTraces(packageVersion);
    }
#endif

    public class GraphQL3Tests : GraphQLTests
    {
        public GraphQL3Tests(ITestOutputHelper output)
            : base("GraphQL3", output, nameof(GraphQL3Tests))
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task SubmitsTraces()
            => await RunSubmitsTraces();
    }

    public class GraphQL2Tests : GraphQLTests
    {
        public GraphQL2Tests(ITestOutputHelper output)
            : base("GraphQL", output, nameof(GraphQL2Tests))
        {
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task SubmitsTraces()
            => await RunSubmitsTraces();
    }

    [UsesVerify]
    public abstract class GraphQLTests : TestHelper
    {
        private const string ServiceVersion = "1.0.0";

        private readonly string _testName;

        protected GraphQLTests(string sampleAppName, ITestOutputHelper output, string testName)
            : base(sampleAppName, output)
        {
            SetServiceVersion(ServiceVersion);

            _testName = testName;
        }

        protected async Task RunSubmitsTraces(string packageVersion = "")
        {
            SetInstrumentationVerification();
            using var telemetry = this.ConfigureTelemetry();
            int? aspNetCorePort = null;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (Process process = StartSample(agent, arguments: null, packageVersion: packageVersion, aspNetCorePort: 0))
            {
                var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        if (args.Data.Contains("Now listening on:"))
                        {
                            var splitIndex = args.Data.LastIndexOf(':');
                            aspNetCorePort = int.Parse(args.Data.Substring(splitIndex + 1));

                            wh.Set();
                        }
                        else if (args.Data.Contains("Unable to start Kestrel"))
                        {
                            wh.Set();
                        }

                        Output.WriteLine($"[webserver][stdout] {args.Data}");
                    }
                };
                process.BeginOutputReadLine();

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        Output.WriteLine($"[webserver][stderr] {args.Data}");
                    }
                };
                process.BeginErrorReadLine();

                wh.WaitOne(15_000);
                if (!aspNetCorePort.HasValue)
                {
                    throw new Exception("Unable to determine port application is listening on");
                }

                Output.WriteLine($"The ASP.NET Core server is ready on port {aspNetCorePort}");

                var expectedSpans = SubmitRequests(aspNetCorePort.Value);

                if (!process.HasExited)
                {
                    // Try shutting down gracefully
                    var shutdownRequest = new RequestInfo() { HttpMethod = "GET", Url = "/shutdown" };
                    SubmitRequest(aspNetCorePort.Value, shutdownRequest);

                    if (!process.WaitForExit(5000))
                    {
                        Output.WriteLine("The process didn't exit in time. Taking proc dump and killing it.");
                        var memoryDumpCaptured = await TakeMemoryDump(process);

                        process.Kill();

                        if (!memoryDumpCaptured)
                        {
                            // if we don't have a memory dump, there's no point continuing.
                            // We know the test will likely fail (telemetry not sent) but it's
                            // not useful to know that, as we don't have a memory dump
                            throw new SkipException("The process didn't exit in time but memory dump couldn't be captured");
                        }
                    }
                }

                var spans = agent.WaitForSpans(expectedSpans);
                var graphQLSpans = spans.Where(s => s.Type == "graphql")
                                        .ToList();

                foreach (var graphQLSpan in graphQLSpans)
                {
                    var result = graphQLSpan.IsGraphQL();
                    Assert.True(result.Success, result.ToString());
                }

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
                                  .UseFileName($"{_testName}.SubmitsTraces{fxSuffix}")
                                  .DisableRequireUniquePrefix(); // all package versions should be the same

                VerifyInstrumentation(process);
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.GraphQL);
        }

        private int SubmitRequests(int aspNetCorePort)
        {
            var expectedGraphQlValidateSpanCount = 0;
            var expectedGraphQlExecuteSpanCount = 0;

            // SUCCESS: query using GET
            SubmitGraphqlRequest(url: "/graphql?query=" + WebUtility.UrlEncode("query{hero{name appearsIn}}"), httpMethod: "GET", graphQlRequestBody: null);

            // SUCCESS: query using POST (default)
            SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""query HeroQuery{hero {name appearsIn}}"",""operationName"": ""HeroQuery""}");

            // SUCCESS: mutation
            SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""mutation AddBobaFett($human:HumanInput!){createHuman(human: $human){id name}}"",""variables"":{""human"":{""name"": ""Boba Fett""}}}");

            // SUCCESS: subscription
            SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{ ""query"":""subscription HumanAddedSub{humanAdded{name}}""}");

            // TODO: When parse is implemented, add a test that fails 'parse' step

            // FAILURE: query fails 'validate' step
            SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""query HumanError{human(id:1){name apearsIn}}""}", failsValidation: true);

            // FAILURE: query fails 'execute' step
            SubmitGraphqlRequest(url: "/graphql", httpMethod: "POST", graphQlRequestBody: @"{""query"":""subscription NotImplementedSub{throwNotImplementedException{name}}""}");

            // TODO: When parse is implemented, add a test that fails 'resolve' step

            return expectedGraphQlExecuteSpanCount + expectedGraphQlValidateSpanCount;

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

                SubmitRequest(
                    aspNetCorePort,
                    new RequestInfo() { Url = url, HttpMethod = httpMethod, RequestBody = graphQlRequestBody, });
            }
        }

        private void SubmitRequest(int aspNetCorePort, RequestInfo requestInfo, bool printResponseText = true)
        {
            try
            {
                var request = WebRequest.Create($"http://localhost:{aspNetCorePort}{requestInfo.Url}");
                request.Method = requestInfo.HttpMethod;
                ((HttpWebRequest)request).UserAgent = "testhelper";

                if (requestInfo.RequestBody != null)
                {
                    byte[] requestBytes = System.Text.Encoding.UTF8.GetBytes(requestInfo.RequestBody);

                    request.ContentType = "application/json";
                    request.ContentLength = requestBytes.Length;

                    using (var dataStream = request.GetRequestStream())
                    {
                        dataStream.Write(requestBytes, 0, requestBytes.Length);
                    }
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string responseText;
                    try
                    {
                        responseText = reader.ReadToEnd();
                    }
                    catch (Exception ex)
                    {
                        responseText = "ENCOUNTERED AN ERROR WHEN READING RESPONSE.";
                        Output.WriteLine(ex.ToString());
                    }

                    if (printResponseText)
                    {
                        Output.WriteLine($"[http] {response.StatusCode} {responseText}");
                    }
                }
            }
            catch (WebException wex)
            {
                Output.WriteLine($"[http] exception: {wex}");
                if (wex.Response is HttpWebResponse response)
                {
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        Output.WriteLine($"[http] {response.StatusCode} {reader.ReadToEnd()}");
                    }
                }
            }
        }

        private class RequestInfo
        {
            public string Url { get; set; }

            public string HttpMethod { get; set; }

            public string RequestBody { get; set; }
        }
    }
}
