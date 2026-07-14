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
    // TriggerAllTimer runs after the script host has initialized the functions and registered their routes,
    // but it can run before the outer HTTP server accepts connections. Polling /admin/host/ping waits for
    // that listener; a 200 response from this endpoint is a liveness signal, not a route-readiness signal.
    //
    // This uses a raw socket so the readiness check doesn't add an http.request client span. The host still
    // records a "GET /admin/host/ping" span, which the integration tests filter out.
    public static async Task WaitForFunctionHostToAcceptHttpRequestsAsync(string baseUrl, int timeoutSeconds = 60)
    {
        var uri = new Uri(baseUrl);
        var request = Encoding.ASCII.GetBytes(
            $"GET /admin/host/ping HTTP/1.1\r\nHost: {uri.Authority}\r\nConnection: close\r\n\r\n");

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            // Bound each attempt so a stalled connect/write/read can't hang until the test's five-minute
            // mutex timeout. Disposing the socket aborts the pending operation if the attempt times out.
            var remaining = deadline - DateTime.UtcNow;
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

        var client = new TcpClient();
        var pingTask = PingHostAsync(client, uri, request);
        var completed = await Task.WhenAny(pingTask, Task.Delay(attemptTimeout));
        if (completed != pingTask)
        {
            client.Dispose();
            ObserveFault(pingTask);
            return false;
        }

        client.Dispose();
        try
        {
            return await pingTask;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (ObjectDisposedException)
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
        await client.ConnectAsync(uri.Host, uri.Port);
        using var stream = client.GetStream();
        await stream.WriteAsync(request, 0, request.Length);

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
