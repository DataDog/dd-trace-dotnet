#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Core.Tools;
using Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class OwinWebApi2WithFeatureFlagTestsCallsite : OwinWebApi2WithFeatureFlagTests
    {
        public OwinWebApi2WithFeatureFlagTestsCallsite(ITestOutputHelper output)
            : base(output, enableCallTarget: false)
        {
        }
    }

    [Collection("IisTests")]
    public class OwinWebApi2WithFeatureFlagTestsCallTarget : OwinWebApi2WithFeatureFlagTests
    {
        public OwinWebApi2WithFeatureFlagTestsCallTarget(ITestOutputHelper output)
            : base(output, enableCallTarget: true)
        {
        }
    }

    public abstract class OwinWebApi2WithFeatureFlagTests : TestHelper
    {
        private readonly TheoryData<string, string, int, bool, string, string, SerializableDictionary> _testData = AspNetWebApi2TestData.WithFeatureFlag;

        public OwinWebApi2WithFeatureFlagTests(ITestOutputHelper output, bool enableCallTarget)
            : base("Owin.WebApi2", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget, enableCallTarget);
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");
            HttpClient = new HttpClient();
        }

        protected HttpClient HttpClient { get; }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            var agentPort = TcpPortProvider.GetOpenPort();
            var aspNetCorePort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (var process = StartSample(agent.Port, arguments: null, packageVersion: string.Empty, aspNetCorePort: aspNetCorePort))
            {
                try
                {
                    agent.SpanFilters.Add(IsNotServerLifeCheck);

                    var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            if (args.Data.Contains("Now listening on:") || args.Data.Contains("Unable to start Kestrel"))
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

                    wh.WaitOne(5000);

                    var maxMillisecondsToWait = 15_000;
                    var intervalMilliseconds = 500;
                    var intervals = maxMillisecondsToWait / intervalMilliseconds;
                    var serverReady = false;

                    // wait for server to be ready to receive requests
                    while (intervals-- > 0)
                    {
                        try
                        {
                            serverReady = await SubmitRequest(aspNetCorePort, "/alive-check") == HttpStatusCode.OK;
                        }
                        catch
                        {
                            // ignore
                        }

                        if (serverReady)
                        {
                            break;
                        }

                        Thread.Sleep(intervalMilliseconds);
                    }

                    if (!serverReady)
                    {
                        throw new Exception("Couldn't verify the application is ready to receive requests.");
                    }

                    var testStart = DateTime.Now;
                    var testDataArray = _testData.ToArray();
                    var expectedSpanCount = testDataArray.Length;

                    foreach (var input in testDataArray)
                    {
                        string path = (string)input[0];
                        await SubmitRequest(aspNetCorePort, path);
                    }

                    var spans =
                        agent.WaitForSpans(
                                  count: expectedSpanCount,
                                  minDateTime: testStart,
                                  returnAllOperations: true)
                             .OrderBy(s => s.Start)
                             .ToList();
                    Assert.Equal(expectedSpanCount, spans.Count);

                    for (int i = 0; i < testDataArray.Length; i++)
                    {
                        object[] input = testDataArray[i];
                        string expectedResourceName = (string)input[1];
                        HttpStatusCode expectedStatusCode = (HttpStatusCode)input[2];
                        bool isError = (bool)input[3];
                        string expectedErrorType = (string)input[4];
                        string expectedErrorMessage = (string)input[5];
                        SerializableDictionary expectedTags = (SerializableDictionary)input[6];

                        MockTracerAgent.Span webApiSpan = spans[i];

                        // base properties
                        Assert.Equal("aspnet-webapi.request", webApiSpan.Name);
                        Assert.Equal("web", webApiSpan.Type);
                        Assert.Equal(expectedResourceName, webApiSpan.Resource);

                        // errors
                        Assert.Equal(isError, webApiSpan.Error == 1); // Fix this. Apparently just returning bad error codes from Owin doesn't set a bad error code on WebApi
                        Assert.Equal(expectedErrorType, webApiSpan.Tags.GetValueOrDefault(Tags.ErrorType));
                        Assert.Equal(expectedErrorMessage, webApiSpan.Tags.GetValueOrDefault(Tags.ErrorMsg));

                        // other tags
                        Assert.Equal(SpanKinds.Server, webApiSpan.Tags.GetValueOrDefault(Tags.SpanKind));
                        Assert.Equal("1.0.0", webApiSpan.Tags.GetValueOrDefault(Tags.Version));

                        if (expectedTags?.Values is not null)
                        {
                            foreach (var expectedTag in expectedTags)
                            {
                                Assert.Equal(expectedTag.Value, webApiSpan.Tags.GetValueOrDefault(expectedTag.Key));
                            }
                        }
                    }
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
            }
        }

        protected async Task<HttpStatusCode> SubmitRequest(int aspNetCorePort, string path)
        {
            HttpResponseMessage response = await HttpClient.GetAsync($"http://localhost:{aspNetCorePort}{path}");
            string responseText = await response.Content.ReadAsStringAsync();
            Output.WriteLine($"[http] {response.StatusCode} {responseText}");
            return response.StatusCode;
        }

        private bool IsNotServerLifeCheck(MockTracerAgent.Span span)
        {
            var url = SpanExpectation.GetTag(span, Tags.HttpUrl);
            if (url == null)
            {
                return true;
            }

            return !url.Contains("alive-check");
        }
    }
}
#endif
