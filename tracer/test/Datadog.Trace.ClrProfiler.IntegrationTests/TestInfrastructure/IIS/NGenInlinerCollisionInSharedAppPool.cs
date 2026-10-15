// <copyright file="NGenInlinerCollisionInSharedAppPool.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.IIS;

/// <summary>
/// The NGenNameHash site runs in the same app pool as the MultipleAppsInDomain sites, and its assembly name hashes
/// like System's. System is domain-neutral under IIS, so looking up the NGen inliners of the site's instrumented
/// methods also decodes NGen inline records of System methods, and decoding them needs a current AppDomain, which
/// the native ReJIT worker doesn't have.
/// Expected behavior: the same worker process serves every request, and the site's methods are instrumented.
/// Wrong behavior: the worker process crashes with an access violation, and IIS starts a new one.
/// </summary>
public class NGenInlinerCollisionInSharedAppPool(ITestOutputHelper output)
{
    private const string Url = "http://localhost:8083";
    private const string WarmupUrl = "http://localhost:8081";
    private const string RewrittenLogEntry = "CallTarget_RewriterCallback() Finished: NGenInlinerCollision.Targets.M0001";

    [SkippableFact]
    [Trait("RunOnWindows", "True")]
    [Trait("IIS", "True")]
    [Trait("MSI", "True")]
    public async Task WorkerProcessDoesNotCrash()
    {
        var intervalMilliseconds = 500;
        var intervals = 5;
        var serverReady = false;
        var client = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30), // yes, this is a long time, but we're running this in CI, in windows containers...
        };

        // The watcher skips what's already logged, so it has to start before the first request starts the site's AppDomain
        var logDirectory = Path.Combine(DatadogLoggingFactory.GetLogDirectory(NullConfigurationTelemetry.Instance), "MultipleAppsInDomain");
        using var logEntryWatcher = new LogEntryWatcher("dotnet-tracer-native-*", logDirectory, output);

        // Warm up the worker process through another site in the app pool. The warmup ignores failed requests, so it
        // must not start this site's AppDomain, which starts the lookup that crashed.
        while (intervals-- > 0)
        {
            try
            {
                output.WriteLine($"Sending warmup request to {WarmupUrl}");
                var serverReadyResponse = await client.GetAsync(WarmupUrl);
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

            await Task.Delay(intervalMilliseconds);
        }

        // The lookup that crashed ran on the ReJIT worker before it requested the ReJIT of the site's methods, and a
        // method is only rewritten on its next call after that request, so keep calling the site until the native log
        // shows the rewrite, then once more. A crash makes a request fail, or IIS serve the next one from a new worker
        // process. Only the IIS worker process's log shows the rewrite, so WaitForLogEntry's timeout reports one that
        // never happens.
        var pid = await GetPid(client);
        var rewritten = logEntryWatcher.WaitForLogEntry(RewrittenLogEntry);
        while (await Task.WhenAny(rewritten, Task.Delay(intervalMilliseconds)) != rewritten)
        {
            (await GetPid(client)).Should().Be(pid);
        }

        await rewritten;
        (await GetPid(client)).Should().Be(pid);
    }

    private async Task<int> GetPid(HttpClient client)
    {
        var responseMessage = await client.GetAsync(Url);
        var response = await responseMessage.Content.ReadAsStringAsync();
        output.WriteLine($"Received response from {Url}: {response}");
        responseMessage.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonConvert.DeserializeObject<Results>(response).Pid;
    }

    public class Results
    {
        public int Pid { get; set; }
    }
}
#endif
