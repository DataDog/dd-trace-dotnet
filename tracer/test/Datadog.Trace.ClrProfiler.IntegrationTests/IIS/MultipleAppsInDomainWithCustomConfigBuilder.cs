// <copyright file="MultipleAppsInDomainWithCustomConfigBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.IIS;

public class MultipleAppsInDomainWithCustomConfigBuilder(ITestOutputHelper output)
{
    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    [Trait("IIS", "True")]
    [Trait("MSI", "True")]
    public async Task ApplicationDoesNotReturnErrors()
    {
        const string App1Url = "http://localhost:8081";
        const string App2Url = "http://localhost:8082";

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
                output.WriteLine($"Sending warmup request to App 1 {App1Url}");
                var serverReadyResponse = await client.GetAsync(App1Url);
                serverReady = serverReadyResponse.StatusCode == HttpStatusCode.OK;
            }
            catch
            {
                // ignore
            }

            if (serverReady)
            {
                output.WriteLine("The server is ready.");
                break;
            }

            Thread.Sleep(intervalMilliseconds);
        }

        // Send request to app 1
        var responseMessage = await client.GetAsync(App1Url);
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await responseMessage.Content.ReadAsStringAsync();
        response.Should().Contain("DummyValue1 - from custom config");

        // Send request to app 2
        responseMessage = await client.GetAsync(App2Url);
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);
        response = await responseMessage.Content.ReadAsStringAsync();
        response.Should().Contain("DummyValue1 - from custom config");
    }
}
