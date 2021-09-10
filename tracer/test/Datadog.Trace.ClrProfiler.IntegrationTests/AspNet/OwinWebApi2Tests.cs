// <copyright file="OwinWebApi2Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class OwinWebApi2TestsCallsite : OwinWebApi2Tests
    {
        public OwinWebApi2TestsCallsite(OwinFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableCallTarget: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class OwinWebApi2TestsCallTarget : OwinWebApi2Tests
    {
        public OwinWebApi2TestsCallTarget(OwinFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableCallTarget: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class OwinWebApi2TestsCallsiteWithFeatureFlag : OwinWebApi2Tests
    {
        public OwinWebApi2TestsCallsiteWithFeatureFlag(OwinFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableCallTarget: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class OwinWebApi2TestsCallTargetWithFeatureFlag : OwinWebApi2Tests
    {
        public OwinWebApi2TestsCallTargetWithFeatureFlag(OwinFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableCallTarget: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [UsesVerify]
    public abstract class OwinWebApi2Tests : TestHelper, IClassFixture<OwinWebApi2Tests.OwinFixture>
    {
        private readonly OwinFixture _fixture;
        private readonly string _testName;
        private readonly ITestOutputHelper _output;

        public OwinWebApi2Tests(OwinFixture fixture, ITestOutputHelper output, bool enableCallTarget, bool enableRouteTemplateResourceNames)
            : base("Owin.WebApi2", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget);
            if (enableRouteTemplateResourceNames)
            {
                SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");
            }

            _fixture = fixture;
            _output = output;
            _testName = nameof(OwinWebApi2Tests)
                      + (enableCallTarget ? ".CallSite" : ".CallTarget")
                      + (enableRouteTemplateResourceNames ? ".NoFF" : ".WithFF");
        }

        public static TheoryData<string, int> Data() => new()
        {
            { "/api/environment", 200 },
            { "/api/absolute-route", 200 },
            { "/api/delay/0", 200 },
            { "/api/delay-optional", 200 },
            { "/api/delay-optional/1", 200 },
            { "/api/delay-async/0", 200 },
            { "/api/transient-failure/true", 200 },
            { "/api/transient-failure/false", 500 },
            { "/api/statuscode/201", 201 },
            { "/api/statuscode/503", 503 },
            { "/api2/delay/0", 200 },
            { "/api2/optional", 200 },
            { "/api2/optional/1", 200 },
            { "/api2/delayAsync/0", 200 },
            { "/api2/transientfailure/true", 200 },
            { "/api2/transientfailure/false", 500 },
            { "/api2/statuscode/201", 201 },
            { "/api2/statuscode/503", 503 },
        };

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(string path, HttpStatusCode statusCode)
        {
            await _fixture.TryStartApp(this, _output);

            var spans = await _fixture.WaitForSpans(Output, path);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }

        public sealed class OwinFixture : IDisposable
        {
            private readonly HttpClient _httpClient;
            private Process _process;

            public OwinFixture()
            {
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
            }

            public MockTracerAgent Agent { get; private set; }

            public int HttpPort { get; private set; }

            public async Task TryStartApp(TestHelper helper, ITestOutputHelper output)
            {
                if (_process is not null)
                {
                    return;
                }

                lock (this)
                {
                    if (_process is null)
                    {
                        var initialAgentPort = TcpPortProvider.GetOpenPort();
                        HttpPort = TcpPortProvider.GetOpenPort();

                        Agent = new MockTracerAgent(initialAgentPort);
                        Agent.SpanFilters.Add(IsNotServerLifeCheck);
                        output.WriteLine($"Starting OWIN sample, agentPort: {Agent.Port}, samplePort: {HttpPort}");
                        _process = helper.StartSample(Agent.Port, arguments: null, packageVersion: string.Empty, aspNetCorePort: HttpPort);
                    }
                }

                await EnsureServerStarted(output);
            }

            public void Dispose()
            {
                lock (this)
                {
                    if (_process is not null)
                    {
                        try
                        {
                            if (!_process.HasExited)
                            {
                                SubmitRequest(null, "/shutdown").GetAwaiter().GetResult();

                                _process.Kill();
                            }
                        }
                        catch
                        {
                            // in some circumstances the HasExited property throws, this means the process probably hasn't even started correctly
                        }

                        _process.Dispose();
                    }
                }

                Agent?.Dispose();
            }

            public async Task<IImmutableList<MockTracerAgent.Span>> WaitForSpans(ITestOutputHelper output, string path)
            {
                var testStart = DateTime.UtcNow;

                await SubmitRequest(output, path);
                return Agent.WaitForSpans(count: 1, minDateTime: testStart, returnAllOperations: true);
            }

            private async Task EnsureServerStarted(ITestOutputHelper output)
            {
                var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

                _process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        if (args.Data.Contains("Webserver started"))
                        {
                            wh.Set();
                        }

                        output.WriteLine($"[webserver][stdout] {args.Data}");
                    }
                };
                _process.BeginOutputReadLine();

                _process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        output.WriteLine($"[webserver][stderr] {args.Data}");
                    }
                };

                _process.BeginErrorReadLine();

                wh.WaitOne(5000);

                var maxMillisecondsToWait = 30_000;
                var intervalMilliseconds = 500;
                var intervals = maxMillisecondsToWait / intervalMilliseconds;
                var serverReady = false;

                // wait for server to be ready to receive requests
                while (intervals-- > 0)
                {
                    try
                    {
                        serverReady = await SubmitRequest(output, "/alive-check") == HttpStatusCode.OK;
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
            }

            private bool IsNotServerLifeCheck(MockTracerAgent.Span span)
            {
                var url = SpanExpectation.GetTag(span, Tags.HttpUrl);
                if (url == null)
                {
                    return true;
                }

                return !url.Contains("alive-check") && !url.Contains("shutdown");
            }

            private async Task<HttpStatusCode> SubmitRequest(ITestOutputHelper output, string path)
            {
                HttpResponseMessage response = await _httpClient.GetAsync($"http://localhost:{HttpPort}{path}");
                string responseText = await response.Content.ReadAsStringAsync();
                output?.WriteLine($"[http] {response.StatusCode} {responseText}");
                return response.StatusCode;
            }
        }
    }
}
#endif
