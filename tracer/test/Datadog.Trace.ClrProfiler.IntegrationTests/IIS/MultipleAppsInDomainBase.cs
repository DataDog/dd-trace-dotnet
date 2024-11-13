// <copyright file="MultipleAppsInDomainBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.IIS;

public abstract class MultipleAppsInDomainBase(ITestOutputHelper output)
{
    protected ITestOutputHelper Output { get; } = output;

    protected async Task RunTest(int app1Port, int app2Port, string expectedOutput, string logDirectoryName)
    {
        var app1Url = $"http://localhost:{app1Port}";
        var app2Url = $"http://localhost:{app2Port}";

        var intervalMilliseconds = 500;
        var intervals = 5;
        var serverReady = false;
        var client = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30), // yes, this is a long time, but we're running this in CI, in windows containers...
        };

        // wait for server to be ready to receive requests
        while (intervals-- > 0)
        {
            try
            {
                Output.WriteLine($"Sending warmup request to App 1 {app1Url}");
                var serverReadyResponse = await client.GetAsync(app1Url);
                serverReady = serverReadyResponse.StatusCode == HttpStatusCode.OK;
            }
            catch
            {
                // ignore
            }

            if (serverReady)
            {
                Output.WriteLine("The server is ready.");
                break;
            }

            Thread.Sleep(intervalMilliseconds);
        }

        // Send request to app 1
        var responseMessage = await client.GetAsync(app1Url);
        var response = await responseMessage.Content.ReadAsStringAsync();
        Output.WriteLine($"Received response from app1 at {app1Url}: {response}");
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);

        var app1Result = JsonConvert.DeserializeObject<Results>(response);
        app1Result.Pid.Should().NotBe(0);
        app1Result.AppConfig.Should().ContainKey("DummyKey1").WhoseValue.Should().Be(expectedOutput);

        // Send request to app 2
        responseMessage = await client.GetAsync(app2Url);
        response = await responseMessage.Content.ReadAsStringAsync();
        Output.WriteLine($"Received response from app2 at {app2Url}: {response}");
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);

        var app2Result = JsonConvert.DeserializeObject<Results>(response);
        app2Result.Pid.Should().Be(app1Result.Pid);
        app2Result.AppConfig.Should().ContainKey("DummyKey1").WhoseValue.Should().Be(expectedOutput);

        // verify we have some logs, so we know instrumentation happened
        var logDirectory = Path.Combine(DatadogLoggingFactory.GetLogDirectory(NullConfigurationTelemetry.Instance), logDirectoryName);
        Output.WriteLine($"Reading files from {logDirectory}");
        Directory.GetFiles(logDirectory).Should().NotBeEmpty();
    }

    public class Results
    {
        public int Pid { get; set; }

        public Dictionary<string, string> AppConfig { get; set; }
    }
}
#endif
