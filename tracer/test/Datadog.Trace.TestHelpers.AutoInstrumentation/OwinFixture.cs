// <copyright file="OwinFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Starts the <c>Samples.Owin.WebApi2</c> sample, which self-hosts Web API with OWIN rather than
    /// running under IIS, and keeps it alive for every test case in a class. Shared by
    /// <c>OwinWebApi2Tests</c> and <c>OtlpOwinWebApi2Tests</c>; each test class gets its own
    /// instance, and so its own process started with that class's configuration.
    /// </summary>
    public sealed class OwinFixture : IAspNetFixture, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly object _initializationLock = new();
        private readonly object _outputLock = new();
        private ITestOutputHelper _currentOutput;
        private Process _process;
        private Task _initialization;

        public OwinFixture()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.TracingEnabled, "false");
            _httpClient.DefaultRequestHeaders.Add(HttpHeaderNames.UserAgent, "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("baggage", "user.id=doggo");
        }

        public MockTracerAgent.TcpUdpAgent Agent { get; private set; }

        public int HttpPort { get; private set; }

        /// <summary>
        /// Gets the ddapm test-agent session the sample exports OTLP to, for suites that use
        /// <c>OTEL_TRACES_EXPORTER=otlp</c>. Owned by the fixture rather than by each test case for the
        /// same reason as <c>IisFixture.OtlpSession</c>: the session token is baked into the sample's
        /// <c>OTEL_EXPORTER_OTLP_HEADERS</c> when the process starts, and that process is shared by
        /// every test case in the class.
        /// </summary>
        public OtlpTestAgentSession OtlpSession { get; } = new();

        /// <inheritdoc />
        public void SetOutput(ITestOutputHelper output)
        {
            lock (_outputLock)
            {
                _currentOutput = output;

                // The agent logs from its own listener threads for as long as the sample runs, so it
                // has to follow the swap rather than hold on to whichever helper was current when it
                // was created. Its own writes are already null-conditional.
                if (Agent is not null)
                {
                    Agent.Output = output;
                }
            }
        }

        /// <inheritdoc />
        public Task EnsureInitializedAsync(Func<Task> initialize)
        {
            lock (_initializationLock)
            {
                if (_initialization is null)
                {
                    try
                    {
                        _initialization = initialize();
                    }
                    catch (Exception ex)
                    {
                        // A delegate that throws before reaching its first await throws out of the
                        // call rather than returning a faulted task, which would leave the latch
                        // unset and send the next test case back through the same failing setup.
                        _initialization = Task.FromException(ex);
                    }
                }

                return _initialization;
            }
        }

        public async Task TryStartApp(TestHelper helper, ITestOutputHelper output)
        {
            SetOutput(output);

            if (_process is not null)
            {
                return;
            }

            var initialAgentPort = TcpPortProvider.GetOpenPort();
            HttpPort = TcpPortProvider.GetOpenPort();

            Agent = MockTracerAgent.Create(_currentOutput, initialAgentPort);
            Agent.SpanFilters.Add(IsNotServerLifeCheck);
            WriteToOutput($"Starting OWIN sample, agentPort: {Agent.Port}, samplePort: {HttpPort}");
            _process = await helper.StartSample(Agent, arguments: null, packageVersion: string.Empty, aspNetCorePort: HttpPort);

            await EnsureServerStarted();
        }

        public void Dispose()
        {
            if (_process is not null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        SubmitRequest("/shutdown").GetAwaiter().GetResult();

                        _process.Kill();
                    }
                }
                catch
                {
                    // in some circumstances the HasExited property throws, this means the process probably hasn't even started correctly
                }

                _process.Dispose();
            }

            Agent?.Dispose();

            SetOutput(null);
        }

        public async Task<IImmutableList<MockSpan>> WaitForSpans(ITestOutputHelper output, string path, int expectedSpanCount)
        {
            SetOutput(output);

            var testStart = DateTimeOffset.UtcNow;

            await SubmitRequest(path);
            return await Agent.WaitForSpansAsync(count: expectedSpanCount, minDateTime: testStart, returnAllOperations: true);
        }

        private async Task EnsureServerStarted()
        {
            var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

            // These handlers stay attached for as long as the sample runs, which is longer than any
            // single test case, so they have to write through WriteToOutput rather than capture the
            // output helper of whichever test case happened to start the sample.
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

                await Task.Delay(intervalMilliseconds);
            }

            if (!serverReady)
            {
                throw new Exception($"Couldn't verify the application is ready to receive requests at http://localhost:{HttpPort}/alive-check.");
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

        private async Task<HttpStatusCode> SubmitRequest(string path)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"http://localhost:{HttpPort}{path}");
            string responseText = await response.Content.ReadAsStringAsync();
            WriteToOutput($"[http] {response.StatusCode} {responseText}");
            return response.StatusCode;
        }

        // The fixture outlives every individual test case, so its diagnostics have to go through
        // whichever ITestOutputHelper is currently accepting writes -- see SetOutput.
        private void WriteToOutput(string line)
        {
            lock (_outputLock)
            {
                try
                {
                    _currentOutput?.WriteLine(line);
                }
                catch (InvalidOperationException)
                {
                    // The test case that owned the output helper finished between the read and the
                    // write. These writes come from the sample's stdout/stderr handlers, which run
                    // on threadpool threads, so throwing here would take the test host down with it.
                }
            }
        }
    }
}
#endif
