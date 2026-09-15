// <copyright file="AzureFunctionsTestHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Samples.AzureFunctions;

internal static class AzureFunctionsTestHelpers
{
    public static async Task WaitForFunctionHostToAcceptHttpRequestsAsync(string baseUrl, int timeoutSeconds = 60)
    {
        var uri = new Uri(baseUrl);

        // HTTP/1.1 requires a Host header; the trailing blank line terminates the headers.
        var request = Encoding.ASCII.GetBytes($"GET /admin/host/ping HTTP/1.1\r\nHost: {uri.Authority}\r\nConnection: close\r\n\r\n");

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;

            // Bound each socket attempt so one stalled operation cannot consume the overall timeout.
            var attemptTimeout = remaining < TimeSpan.FromSeconds(5) ? remaining : TimeSpan.FromSeconds(5);

            if (await TryPingHostAsync(uri, request, attemptTimeout))
            {
                return;
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Azure Functions host at {baseUrl} did not accept HTTP requests within {timeoutSeconds}s.");
    }

    private static async Task<bool> TryPingHostAsync(Uri uri, byte[] request, TimeSpan attemptTimeout)
    {
        if (attemptTimeout <= TimeSpan.Zero)
        {
            return false;
        }

        using var client = new TcpClient();
        try
        {
            var pingTask = PingHostAsync(client, uri, request);
            if (await Task.WhenAny(pingTask, Task.Delay(attemptTimeout)) != pingTask)
            {
                ObserveFault(pingTask);
                return false;
            }

            return await pingTask;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void ObserveFault(Task task)
        => task.ContinueWith(
            t => { _ = t.Exception; },
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

    private static async Task<bool> PingHostAsync(TcpClient client, Uri uri, byte[] request)
    {
        // Use raw TCP to avoid creating an outgoing HttpClient span.
        await client.ConnectAsync(uri.Host, uri.Port);
        using var stream = client.GetStream();
        await stream.WriteAsync(request);

        using var reader = new StreamReader(stream, Encoding.ASCII);
        var statusLine = await reader.ReadLineAsync();
        if (statusLine is null)
        {
            return false;
        }

        var parts = statusLine.Split(' ');
        return parts.Length >= 2 && parts[1] == "200";
    }
}
