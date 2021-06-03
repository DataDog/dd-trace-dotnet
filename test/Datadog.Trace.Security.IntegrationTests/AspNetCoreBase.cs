// <copyright file="AspNetCoreBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCoreBase : IDisposable
    {
        private readonly HttpClient httpClient;
        private int httpPort;
        private Process process;

        public AspNetCoreBase(string sampleName, ITestOutputHelper outputHelper, string samplesDir = null)
        {
            Output = outputHelper;
            httpClient = new HttpClient();
            EnvironmentHelper = new EnvironmentHelper(sampleName, typeof(AspNetCoreBase), Output, samplesDirectory: samplesDir ?? "test/test-applications/security");
        }

        public EnvironmentHelper EnvironmentHelper { get; }

        protected ITestOutputHelper Output { get; }

        public Task RunOnSelfHosted(bool enableSecurity)
        {
            var agentPort = TcpPortProvider.GetOpenPort();
            httpPort = TcpPortProvider.GetOpenPort();

            using var agent = new MockTracerAgent(agentPort);
            return StartSample(agent.Port, arguments: null, aspNetCorePort: httpPort, enableSecurity: enableSecurity);
        }

        public Task RunOnIis(string path, bool enableSecurity)
        {
            var initialAgentPort = TcpPortProvider.GetOpenPort();
            var agent = new MockTracerAgent(initialAgentPort);
            httpPort = TcpPortProvider.GetOpenPort();

            var arguments = $"/clr:v4.0 /path:{EnvironmentHelper.GetSampleProjectDirectory()} /systray:false /port:{httpPort} /trace:verbose";
            Output.WriteLine($"[webserver] starting {path} {string.Join(" ", arguments)}");

            return StartSample(agent.Port, arguments, httpPort, iisExpress: true, enableSecurity: enableSecurity);
        }

        public void Dispose()
        {
            if (process != null && !process.HasExited)
            {
                Output.WriteLine("Killing process");
                process.Kill();
                process.Dispose();
            }
        }

        protected async Task<(HttpStatusCode StatusCode, string ResponseText)> SubmitRequest(string path)
        {
            var response = await httpClient.GetAsync($"http://localhost:{this.httpPort}{path}");
            var responseText = await response.Content.ReadAsStringAsync();
            Output.WriteLine($"[http] {response.StatusCode} {responseText}");
            return (response.StatusCode, responseText);
        }

        private async Task StartSample(int traceAgentPort, string arguments, int? aspNetCorePort = null, string packageVersion = "", int? statsdPort = null, string framework = "", bool iisExpress = false, string path = "/Home", bool enableSecurity = true)
        {
            var sampleAppPath = string.Empty;
            // get path to sample app that the profiler will attach to
            const int mstimeout = 5000;
            var message = "Now listening on:";

            if (iisExpress)
            {
                message = "IIS Express is running.";
                sampleAppPath = EnvironmentHelper.GetSampleExecutionSource();
            }
            else
            {
                sampleAppPath = EnvironmentHelper.GetSampleApplicationPath(packageVersion, framework);
            }

            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            // get full paths to integration definitions
            var integrationPaths = Directory.EnumerateFiles(".", "*integrations.json").Select(Path.GetFullPath);

            process = ProfilerHelper.StartProcessWithProfiler(
                EnvironmentHelper.GetSampleExecutionSource(),
                EnvironmentHelper,
                $"{sampleAppPath} {arguments ?? string.Empty}",
                traceAgentPort: traceAgentPort,
                statsdPort: statsdPort,
                aspNetCorePort: aspNetCorePort.GetValueOrDefault(5000),
                enableSecurity: enableSecurity,
                callTargetEnabled: true);

            // then wait server ready
            var wh = new EventWaitHandle(false, EventResetMode.AutoReset);
            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    if (args.Data.Contains(message))
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

            wh.WaitOne(mstimeout);

            var maxMillisecondsToWait = 15_000;
            var intervalMilliseconds = 500;
            var intervals = maxMillisecondsToWait / intervalMilliseconds;
            var serverReady = false;
            var responseText = string.Empty;

            // wait for server to be ready to receive requests
            while (intervals-- > 0)
            {
                var response = await SubmitRequest(path);
                responseText = response.ResponseText;
                serverReady = response.StatusCode == HttpStatusCode.OK;

                if (serverReady)
                {
                    break;
                }

                Thread.Sleep(intervalMilliseconds);
            }

            if (!serverReady)
            {
                process.Kill();
                throw new Exception($"Couldn't verify the application is ready to receive requests: {responseText}");
            }
        }
    }
}
