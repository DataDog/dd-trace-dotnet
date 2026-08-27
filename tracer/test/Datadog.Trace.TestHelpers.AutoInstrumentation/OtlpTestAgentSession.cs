// <copyright file="OtlpTestAgentSession.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers;

/// <summary>
/// Shared plumbing for the tests that export OTLP to the ddapm test-agent and read it back out
/// of the test-agent's session: where the test agent is, how the application under test is
/// pointed at it (<see cref="TestHelper.ConfigureOtlpExport"/>), and how the session is isolated
/// per test case. Shaping the payloads that come back out is a separate concern, handled by the
/// consuming test project.
/// <para>
/// Applications that exit before their telemetry is read (the console samples) only need
/// <see cref="ClearSessionAsync"/>. A server that outlives a single test case -- an IIS site
/// behind <c>IisFixture</c>, or a Kestrel process behind <c>AspNetCoreTestFixture</c> -- needs
/// <see cref="ClearSessionWhenQuietAsync"/> instead, and also
/// <see cref="CheckAvailabilityAsync"/>, since both fixtures have to be started without their
/// own health check (a request round-trip through the mock DD agent) once
/// <c>OTEL_TRACES_EXPORTER=otlp</c> routes every span to this session instead.
/// </para>
/// <para>
/// Test classes that use this should carry <c>[Collection(nameof(TestAgentOtlpCollection))]</c>,
/// because clearing the session affects every other test reading from the same ddapm test-agent.
/// </para>
/// </summary>
internal sealed class OtlpTestAgentSession
{
    /// <summary>
    /// The port the ddapm test-agent receives OTLP/HTTP on, and also serves its session API on.
    /// </summary>
    private const int HttpPort = 4318;

    /// <summary>
    /// The port the ddapm test-agent receives OTLP/gRPC on.
    /// </summary>
    private const int GrpcPort = 4317;

    /// <summary>
    /// How many times a clear request is retried, to cover the test agent still starting up.
    /// </summary>
    private const int MaxClearAttempts = 5;

    private const int ClearRetryDelayMs = 1_000;

    /// <summary>
    /// How long the session has to stay empty after being cleared before we accept that nothing
    /// else is in flight. Needs to comfortably exceed the exporter's flush interval, including on
    /// contended ARM64 CI runners where a batch export can lag well past the exporter's nominal
    /// flush interval.
    /// </summary>
    private const int QuietPeriodMs = 5_000;

    private const int MaxQuietAttempts = 6;

    /// <summary>
    /// How long to keep polling for the telemetry a test case is waiting on. Generous because a
    /// console sample only exports during shutdown, and a first-time gRPC connection
    /// (TCP+HTTP/2+TLS handshake) plus that shutdown flush can stack up on slower CI runners.
    /// </summary>
    private const int WaitTimeoutSeconds = 60;

    private const int PollIntervalMs = 500;

    public OtlpTestAgentSession()
    {
        TestAgentHost = Environment.GetEnvironmentVariable("TEST_AGENT_HOST") ?? "127.0.0.1";
        SessionToken = Guid.NewGuid().ToString();
        TracesUrl = GetSessionUrl("traces");
        MetricsUrl = GetSessionUrl("metrics");
        LogsUrl = GetSessionUrl("logs");
    }

    public string TestAgentHost { get; }

    /// <summary>
    /// Gets the token that scopes this instance's traffic to its own session on the ddapm
    /// test-agent, via the <c>X-Datadog-Test-Session-Token</c> header, so that concurrent test
    /// runs sharing one test-agent (e.g. across CI jobs pointed at the same container) don't see
    /// each other's spans. The application under test must send this header on every request it
    /// makes to the test-agent; see <see cref="TestHelper.ConfigureOtlpExport"/>.
    /// </summary>
    public string SessionToken { get; }

    /// <summary>
    /// Gets the test-agent endpoint this session's spans are read from.
    /// </summary>
    public string TracesUrl { get; }

    /// <summary>
    /// Gets the test-agent endpoint this session's metrics are read from.
    /// </summary>
    public string MetricsUrl { get; }

    /// <summary>
    /// Gets the test-agent endpoint this session's logs are read from.
    /// </summary>
    public string LogsUrl { get; }

    /// <summary>
    /// Gets a value indicating whether the test-agent was reachable the last time
    /// <see cref="CheckAvailabilityAsync"/> ran. Callers should skip rather than fail their tests
    /// when this is <c>false</c>, since some CI jobs run without a ddapm test-agent available.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Counts the spans in a set of captured OTLP requests, across every resource and scope.
    /// </summary>
    /// <param name="tracesRequests">The captured OTLP requests.</param>
    /// <returns>The number of spans.</returns>
    public static int CountSpans(JToken tracesRequests) => tracesRequests.SelectTokens("$..spans[*]").Count();

    /// <summary>
    /// Gets the OTLP endpoint the application under test exports to, which is the receiver
    /// matching <paramref name="protocol"/> rather than the session API the tests read from.
    /// </summary>
    /// <param name="protocol">The <c>OTEL_EXPORTER_OTLP_PROTOCOL</c> value in play.</param>
    /// <returns>The value for <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>.</returns>
    public string GetExporterEndpoint(string protocol) =>
        $"http://{TestAgentHost}:{(protocol == "grpc" ? GrpcPort : HttpPort)}";

    /// <summary>
    /// Checks whether the ddapm test-agent is reachable, and records the result in
    /// <see cref="IsAvailable"/>. Call once, from the fixture's <c>InitializeAsync</c>, before
    /// paying for starting the (potentially heavyweight) application under test.
    /// </summary>
    /// <param name="output">Where to log the failure when the test-agent isn't reachable.</param>
    /// <returns><see langword="true"/> when the test-agent responded.</returns>
    public async Task<bool> CheckAvailabilityAsync(ITestOutputHelper? output = null)
    {
        try
        {
            // Establishes this token as a real ddapm test-agent session, so that
            // /test/session/traces only ever returns requests sent after this point.
            await StartSessionAsync();
            await GetDataAsync(TracesUrl, SessionToken);
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            output?.WriteLine($"[test-agent] not reachable at {TracesUrl}: {ex.Message}");
            IsAvailable = false;
        }

        return IsAvailable;
    }

    // Marks the start of this token's session.
    public async Task StartSessionAsync()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        httpClient.DefaultRequestHeaders.Add("X-Datadog-Test-Session-Token", SessionToken);
        var response = await httpClient.GetAsync(GetSessionUrl("start"));
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Clears the test-agent session, so this test case's telemetry is the only thing in it,
    /// retrying while the test agent is still starting up. Enough for an application that is
    /// started after this runs and has exited by the time the session is read; see
    /// <see cref="ClearSessionWhenQuietAsync"/> when the application outlives the test case.
    /// </summary>
    /// <returns>A task that completes once the session has been cleared.</returns>
    public async Task ClearSessionAsync()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        httpClient.DefaultRequestHeaders.Add("X-Datadog-Test-Session-Token", SessionToken);
        var url = GetSessionUrl("clear");

        for (var attempt = 1; attempt <= MaxClearAttempts; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return;
            }
            catch (Exception) when (attempt < MaxClearAttempts)
            {
                await Task.Delay(ClearRetryDelayMs);
            }
        }
    }

    /// <summary>
    /// Clears the test-agent session and confirms it stays empty, so the next test case's
    /// snapshot only contains the spans it produces itself. Needed because the server is shared
    /// by every test case in the class, so spans from the previous case (or from warming the
    /// application up) can still be in flight when this one starts.
    /// </summary>
    /// <param name="output">Where to log retries while the session settles.</param>
    public async Task ClearSessionWhenQuietAsync(ITestOutputHelper? output = null)
    {
        for (var attempt = 1; attempt <= MaxQuietAttempts; attempt++)
        {
            await ClearSessionAsync();
            await Task.Delay(QuietPeriodMs);

            var traces = await GetDataAsync(TracesUrl, SessionToken);
            if (!traces.HasValues)
            {
                return;
            }

            output?.WriteLine($"[test-agent] {CountSpans(traces)} span(s) arrived after clearing the session (attempt {attempt} of {MaxQuietAttempts}), retrying.");
        }

        throw new InvalidOperationException(
            $"The test-agent session kept receiving spans after being cleared {MaxQuietAttempts} times, so this test case's spans can't be isolated.");
    }

    /// <summary>
    /// Polls this session until it holds at least <paramref name="expectedSpanCount"/> spans.
    /// Waiting on a count rather than on "any traces at all" is what an application that keeps
    /// running needs, since the exporter can flush a partial view of the trace.
    /// </summary>
    /// <param name="expectedSpanCount">The number of spans the request under test is expected to produce.</param>
    /// <returns>The captured OTLP trace requests.</returns>
    public Task<JToken> WaitForSpansAsync(int expectedSpanCount) =>
        WaitForDataAsync(TracesUrl, data => CountSpans(data) >= expectedSpanCount);

    /// <summary>
    /// Polls this session until any traces arrive. Enough for an application that has already
    /// exited, because everything it produced was flushed during its shutdown.
    /// </summary>
    /// <returns>The captured OTLP trace requests.</returns>
    public Task<JToken> WaitForTracesAsync() => WaitForDataAsync(TracesUrl, data => data.HasValues);

    /// <summary>
    /// Polls this session until any metrics arrive.
    /// </summary>
    /// <returns>The captured OTLP metrics requests.</returns>
    public Task<JToken> WaitForMetricsAsync() => WaitForDataAsync(MetricsUrl, data => data.HasValues);

    /// <summary>
    /// Polls this session until any logs arrive.
    /// </summary>
    /// <returns>The captured OTLP logs requests.</returns>
    public Task<JToken> WaitForLogsAsync() => WaitForDataAsync(LogsUrl, data => data.HasValues);

    // Reads one of this session's endpoints, without waiting for anything to arrive.
    private static async Task<JToken> GetDataAsync(string url, string sessionToken)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        httpClient.DefaultRequestHeaders.Add("X-Datadog-Test-Session-Token", sessionToken);
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JToken.Parse(json);
    }

    // Polls one of this session's endpoints until isComplete is satisfied or the timeout is
    // reached. On timeout the last response is returned rather than thrown on, so the caller's
    // assertion reports the actual payload.
    private async Task<JToken> WaitForDataAsync(string url, Func<JToken, bool> isComplete)
    {
        var deadline = DateTime.UtcNow.AddSeconds(WaitTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            var data = await GetDataAsync(url, SessionToken);

            if (isComplete(data))
            {
                return data;
            }

            await Task.Delay(PollIntervalMs);
        }

        // Final attempt -- return whatever we get so the caller's assertion shows the actual value
        return await GetDataAsync(url, SessionToken);
    }

    private string GetSessionUrl(string kind) => $"http://{TestAgentHost}:{HttpPort}/test/session/{kind}";
}
