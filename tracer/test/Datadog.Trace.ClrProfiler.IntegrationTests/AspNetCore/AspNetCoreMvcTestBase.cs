// <copyright file="AspNetCoreMvcTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    [UsesVerify]
    public abstract class AspNetCoreMvcTestBase : TestHelper, IClassFixture<AspNetCoreMvcTestBase.AspNetCoreTestFixture>, IDisposable
    {
        protected const string HeaderName1WithMapping = "datadog-header-name";
        protected const string HeaderName1UpperWithMapping = "DATADOG-HEADER-NAME";
        protected const string HeaderTagName1WithMapping = "datadog-header-tag";
        protected const string HeaderValue1 = "asp-net-core";
        protected const string HeaderName2 = "sample.correlation.identifier";
        protected const string HeaderValue2 = "0000-0000-0000";
        protected const string HeaderName3 = "Server";
        protected const string HeaderValue3 = "Kestrel";

        private readonly bool _enableRouteTemplateResourceNames;

        protected AspNetCoreMvcTestBase(string sampleName, AspNetCoreTestFixture fixture, ITestOutputHelper output, bool enableRouteTemplateResourceNames)
            : base(sampleName, output)
        {
            _enableRouteTemplateResourceNames = enableRouteTemplateResourceNames;
            SetEnvironmentVariable(ConfigurationKeys.HeaderTags, $"{HeaderName1UpperWithMapping}:{HeaderTagName1WithMapping},{HeaderName2},{HeaderName3}");
            SetEnvironmentVariable(ConfigurationKeys.HttpServerErrorStatusCodes, "400-403, 500-503");

            SetServiceVersion("1.0.0");

            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());

            Fixture = fixture;
            Fixture.SetOutput(output);
        }

        protected AspNetCoreTestFixture Fixture { get; }

        public static TheoryData<string, int> Data() => new()
        {
            { "/", 200 },
            { "/delay/0", 200 },
            { "/api/delay/0", 200 },
            { "/not-found", 404 },
            { "/status-code/203", 203 },
            { "/status-code/500", 500 },
            { "/status-code-string/[200]", 500 },
            { "/bad-request", 500 },
            { "/status-code/402", 402 },
            { "/ping", 200 },
            { "/branch/ping", 200 },
            { "/branch/not-found", 404 },
            { "/handled-exception", 500 },
        };

        public void Dispose()
        {
            Fixture.SetOutput(null);
        }

        protected string GetTestName(string testName)
        {
            return testName
                 + (_enableRouteTemplateResourceNames ? ".WithFF" : ".NoFF");
        }

        public sealed class AspNetCoreTestFixture : IDisposable
        {
            private readonly HttpClient _httpClient;
            private Process _process;
            private ITestOutputHelper _currentOutput;

            public AspNetCoreTestFixture()
            {
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
                _httpClient.DefaultRequestHeaders.Add(HeaderName1WithMapping, HeaderValue1);
                _httpClient.DefaultRequestHeaders.Add(HeaderName2, HeaderValue2);
            }

            public MockTracerAgent.TcpUdpAgent Agent { get; private set; }

            public int HttpPort { get; private set; }

            public void SetOutput(ITestOutputHelper output)
            {
                lock (this)
                {
                    _currentOutput = output;
                }
            }

            public async Task TryStartApp(TestHelper helper)
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

                        Agent = MockTracerAgent.Create(initialAgentPort);
                        Agent.SpanFilters.Add(IsNotServerLifeCheck);
                        WriteToOutput($"Starting aspnetcore sample, agentPort: {Agent.Port}, samplePort: {HttpPort}");
                        _process = helper.StartSample(Agent, arguments: null, packageVersion: string.Empty, aspNetCorePort: HttpPort);
                    }
                }

                await EnsureServerStarted();
            }

            public void Dispose()
            {
                var request = WebRequest.CreateHttp($"http://localhost:{HttpPort}/shutdown");
                request.GetResponse().Close();

                if (_process is not null)
                {
                    try
                    {
                        if (!_process.HasExited)
                        {
                            if (!_process.WaitForExit(5000))
                            {
                                _process.Kill();
                            }
                        }
                    }
                    catch
                    {
                        // in some circumstances the HasExited property throws, this means the process probably hasn't even started correctly
                    }

                    _process.Dispose();
                }

                Agent?.Dispose();
            }

            public async Task<IImmutableList<MockSpan>> WaitForSpans(string path, bool post = false)
            {
                var testStart = DateTime.UtcNow;

                await SubmitRequest(path, post);
                return Agent.WaitForSpans(count: 1, minDateTime: testStart, returnAllOperations: true);
            }

            private async Task EnsureServerStarted()
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

                        WriteToOutput($"[webserver][stdout] {args.Data}");
                    }
                };
                _process.BeginOutputReadLine();

                _process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        WriteToOutput($"[webserver][stderr] {args.Data}");
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
                        serverReady = await SubmitRequest("/alive-check") == HttpStatusCode.OK;
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

            private bool IsNotServerLifeCheck(MockSpan span)
            {
                span.Tags.TryGetValue(Tags.HttpUrl, out var url);
                if (url == null)
                {
                    return true;
                }

                return !url.Contains("alive-check") && !url.Contains("shutdown");
            }

            private async Task<HttpStatusCode> SubmitRequest(string path, bool post = false)
            {
                HttpResponseMessage response;
                if (!post)
                {
                    response = await _httpClient.GetAsync($"http://localhost:{HttpPort}{path}");
                }
                else
                {
                    response = await _httpClient.PostAsync($"http://localhost:{HttpPort}{path}", null);
                }

                string responseText = await response.Content.ReadAsStringAsync();
                WriteToOutput($"[http] {response.StatusCode} {responseText}");
                return response.StatusCode;
            }

            private void WriteToOutput(string line)
            {
                lock (this)
                {
                    _currentOutput?.WriteLine(line);
                }
            }
        }
    }
}
#endif
