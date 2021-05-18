using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCoreBase
    {
        private readonly ITestOutputHelper output;
        private readonly HttpClient httpClient;
        private int aspNetCorePort;

        public AspNetCoreBase(string sampleName, ITestOutputHelper outputHelper)
        {
            output = outputHelper;
            httpClient = new HttpClient();
            EnvironmentHelper = new EnvironmentHelper(sampleName, typeof(AspNetCoreBase), output, samplesDirectory: "test/test-applications/security");
        }

        public EnvironmentHelper EnvironmentHelper { get; }

        public async Task<Process> RunTraceTestOnSelfHosted(string path)
        {
            var agentPort = TcpPortProvider.GetOpenPort();
            aspNetCorePort = TcpPortProvider.GetOpenPort();

            using var agent = new MockTracerAgent(agentPort);
            using var process = StartSample(agent.Port, arguments: null, string.Empty, aspNetCorePort: aspNetCorePort);

            var wh = new EventWaitHandle(false, EventResetMode.AutoReset);

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    if (args.Data.Contains("Now listening on:") || args.Data.Contains("Unable to start Kestrel"))
                    {
                        wh.Set();
                    }

                    output.WriteLine($"[webserver][stdout] {args.Data}");
                }
            };
            process.BeginOutputReadLine();

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    output.WriteLine($"[webserver][stderr] {args.Data}");
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
                serverReady = (await SubmitRequest(path)).StatusCode == HttpStatusCode.OK;

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
            return process;
        }

        public Process StartSample(int traceAgentPort, string arguments, string packageVersion, int aspNetCorePort, int? statsdPort = null, string framework = "")
        {
            // get path to sample app that the profiler will attach to
            var sampleAppPath = EnvironmentHelper.GetSampleApplicationPath(packageVersion, framework);
            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            // get full paths to integration definitions
            var integrationPaths = Directory.EnumerateFiles(".", "*integrations.json").Select(Path.GetFullPath);

            return ProfilerHelper.StartProcessWithProfiler(
                EnvironmentHelper.GetSampleExecutionSource(),
                sampleAppPath,
                EnvironmentHelper,
                integrationPaths,
                arguments,
                traceAgentPort: traceAgentPort,
                statsdPort: statsdPort,
                aspNetCorePort: aspNetCorePort);
        }

        protected async Task SubmitRequests(params string[] paths)
        {
            foreach (var path in paths)
            {
                await SubmitRequest(path);
            }
        }

        protected async Task<(HttpStatusCode StatusCode, string ResponseText)> SubmitRequest(string path)
        {
            var response = await httpClient.GetAsync($"http://localhost:{this.aspNetCorePort}{path}");
            var responseText = await response.Content.ReadAsStringAsync();
            output.WriteLine($"[http] {response.StatusCode} {responseText}");
            return (response.StatusCode, responseText);
        }
    }
}
