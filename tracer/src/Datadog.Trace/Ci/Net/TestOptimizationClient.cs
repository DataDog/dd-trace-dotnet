// <copyright file="TestOptimizationClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

// ReSharper disable UnusedType.Local
// ReSharper disable UnusedMember.Local
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable NotAccessedField.Local

namespace Datadog.Trace.Ci.Net;

internal sealed partial class TestOptimizationClient : ITestOptimizationClient
{
    private const string ApiKeyHeader = "DD-API-KEY";
    private const string EvpSubdomainHeader = "X-Datadog-EVP-Subdomain";

    private const int MaxRetries = 5;
    private const int MaxPackFileSizeInMb = 3;

    private static readonly IDatadogLogger Log;
    private static readonly Regex ShaRegex;
    private static readonly JsonSerializerSettings SerializerSettings;

    private readonly string _workingDirectory;
    private readonly ITestOptimization _testOptimization;

    private readonly string _id;
    private readonly string _environment;
    private readonly string _serviceName;
    private readonly Dictionary<string, string>? _customConfigurations;
    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly EventPlatformProxySupport _eventPlatformProxySupport;
    private readonly string _repositoryUrl;
    private readonly string _branchName;
    private readonly string _commitSha;

    static TestOptimizationClient()
    {
        Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationClient));
        ShaRegex = new("[0-9a-f]+", RegexOptions.Compiled);
        SerializerSettings = new() { DefaultValueHandling = DefaultValueHandling.Ignore };
    }

    private TestOptimizationClient(string workingDirectory, ITestOptimization testOptimization)
    {
        _id = RandomIdGenerator.Shared.NextSpanId().ToString(CultureInfo.InvariantCulture);
        _testOptimization = testOptimization;
        var settings = _testOptimization.Settings;

        _workingDirectory = workingDirectory;
        _environment = TraceUtil.NormalizeTag(settings.TracerSettings.MutableSettings.Environment ?? "none") ?? "none";
        _serviceName = NormalizerTraceProcessor.NormalizeService(settings.TracerSettings.MutableSettings.ServiceName) ?? string.Empty;

        // Extract custom tests configurations from DD_TAGS
        _customConfigurations = GetCustomTestsConfigurations(settings.TracerSettings.MutableSettings.GlobalTags);

        _apiRequestFactory = _testOptimization.TracerManagement!.GetRequestFactory(settings.TracerSettings, TimeSpan.FromSeconds(45));
        _eventPlatformProxySupport = settings.Agentless ? EventPlatformProxySupport.None : _testOptimization.TracerManagement.EventPlatformProxySupport;

        var ciValues = testOptimization.CIValues;
        _repositoryUrl = ciValues.Repository ?? string.Empty;
        _commitSha = ciValues.Commit ?? string.Empty;
        _branchName = ciValues switch
        {
            // we try to get the branch name
            { Branch: { Length: > 0 } branch } => branch,
            // if not we try to use the tag (checkout over a tag)
            { Tag: { Length: > 0 } tag } => tag,
            // if is still empty we assume the customer just used a detached HEAD
            _ => "auto:git-detached-head"
        };
    }

    public static ITestOptimizationClient Create(string workingDirectory, ITestOptimization testOptimization)
    {
        return new TestOptimizationClient(workingDirectory, testOptimization);
    }

    public static ITestOptimizationClient CreateCached(string workingDirectory, ITestOptimization testOptimization)
    {
        return new CachedTestOptimizationClient(new TestOptimizationClient(workingDirectory, testOptimization));
    }

    internal static Dictionary<string, string>? GetCustomTestsConfigurations(IReadOnlyDictionary<string, string> globalTags)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (globalTags is null)
        {
            return null;
        }

        Dictionary<string, string>? customConfiguration = null;
        foreach (var tag in globalTags)
        {
            const string testConfigKey = "test.configuration.";
            if (tag.Key.StartsWith(testConfigKey, StringComparison.OrdinalIgnoreCase))
            {
                var key = tag.Key.Substring(testConfigKey.Length);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                customConfiguration ??= new Dictionary<string, string>();
                customConfiguration[key] = tag.Value;
            }
        }

        return customConfiguration;
    }

#if NETFRAMEWORK
    private static ServicePoint ConfigureServicePoint(Uri baseUri)
    {
        /*
         This helper shapes how .NET's ServicePoint pool behaves for the endpoint. We call it before
         each request so that, regardless of when the ServicePoint was first created, the lease/idle thresholds
         reflect the values we want. ConnectionLeaseTimeout tells the runtime to retire sockets that have lived
         longer than the configured window the next time they transition back to idle, which forces the following
         request to open a new TCP connection instead of reusing a potentially stale keep-alive that the load
         balancer already closed. MaxIdleTime complements that by expiring sockets that sit idle for more than
         20 seconds, preventing us from reviving a broken connection even if the total lifetime is below the lease.
         Expect100Continue and Nagle are disabled per ServicePoint to avoid extra round-trips and delays that can
         surface when the proxy/LB behaves differently across connections. If we still observe a keep-alive failure
         despite these proactive limits, CloseConnectionGroupOnFailure tears down the pool immediately before the
         retry so that the next attempt is guaranteed to create a fresh socket.
        */

        var sp = ServicePointManager.FindServicePoint(baseUri);

        // Kill “zombie” keep-alives proactively
        sp.ConnectionLeaseTimeout = (int)TimeSpan.FromSeconds(25).TotalMilliseconds; // < typical 30s LB idle
        sp.MaxIdleTime = (int)TimeSpan.FromSeconds(20).TotalMilliseconds; // drop idle sockets quickly

        // Reduce proxy quirks / latency micro-hiccups
        sp.Expect100Continue = false;  // per-ServicePoint (not global)
        sp.UseNagleAlgorithm = false;  // optional: lowers small-write delays

        // Keep enough lanes open to avoid request queueing
        sp.ConnectionLimit = Math.Max(64, Environment.ProcessorCount * 8);
        return sp;
    }

    private static void CloseConnectionGroupOnFailure(Uri baseUri, Exception exception)
    {
        /*
         When the agent or an intermediate proxy closes the TCP connection while we still have it in the
         ServicePoint pool, the next reuse attempt faults before we can refresh the socket. By explicitly closing
         the connection group on these WebException statuses we drop every cached socket for the target endpoint,
         guaranteeing that the retry path rebuilds the connection from scratch (DNS, TCP handshake, TLS) instead
         of surfacing "connection was closed unexpectedly" repeatedly.
        */

        if (exception is not WebException webException)
        {
            return;
        }

        switch (webException.Status)
        {
            case WebExceptionStatus.ConnectionClosed:
            case WebExceptionStatus.KeepAliveFailure:
            case WebExceptionStatus.ReceiveFailure:
                try
                {
                    var servicePoint = ServicePointManager.FindServicePoint(baseUri);
                    servicePoint?.CloseConnectionGroup(string.Empty);
                    Log.Debug("TestOptimizationClient: Closed ServicePoint connection group after keep-alive failure.");
                }
                catch (Exception closeEx)
                {
                    Log.Debug(closeEx, "TestOptimizationClient: Failed to close ServicePoint connection group.");
                }

                break;
        }
    }
#endif

    private bool EnsureRepositoryUrl()
    {
        if (string.IsNullOrEmpty(_repositoryUrl))
        {
            Log.Warning("TestOptimizationClient: Repository url cannot be retrieved, command returned null or empty");
            return false;
        }

        return true;
    }

    private bool EnsureBranchName()
    {
        if (string.IsNullOrEmpty(_branchName))
        {
            Log.Warning("TestOptimizationClient: Branch name cannot be retrieved, command returned null or empty");
            return false;
        }

        return true;
    }

    private bool EnsureCommitSha()
    {
        if (string.IsNullOrEmpty(_commitSha))
        {
            Log.Warning("TestOptimizationClient: Commit sha cannot be retrieved, command returned null or empty");
            return false;
        }

        return true;
    }

    private TestsConfigurations GetTestConfigurations(bool skipFrameworkInfo = false)
    {
        var framework = FrameworkDescription.Instance;
        return new TestsConfigurations(
            framework.OSPlatform,
            _testOptimization.HostInfo.GetOperatingSystemVersion(),
            framework.OSArchitecture,
            skipFrameworkInfo ? null : framework.Name,
            skipFrameworkInfo ? null : framework.ProductVersion,
            skipFrameworkInfo ? null : framework.ProcessArchitecture,
            _customConfigurations);
    }

    private Uri GetUriFromPath(string uriPath)
    {
        var settings = _testOptimization.Settings;
        if (settings.Agentless)
        {
            var agentlessUrl = settings.AgentlessUrl;
            if (!string.IsNullOrWhiteSpace(agentlessUrl))
            {
                return new UriBuilder(agentlessUrl) { Path = uriPath }.Uri;
            }

            return new UriBuilder(
                scheme: "https",
                host: "api." + settings.Site,
                port: 443,
                pathValue: uriPath).Uri;
        }

        switch (_eventPlatformProxySupport)
        {
            case EventPlatformProxySupport.V2:
                return _apiRequestFactory.GetEndpoint($"evp_proxy/v2/{uriPath}");
            case EventPlatformProxySupport.V4:
                return _apiRequestFactory.GetEndpoint($"evp_proxy/v4/{uriPath}");
            default:
                throw new NotSupportedException("Event platform proxy not supported by the agent.");
        }
    }

    private async Task<string> SendJsonRequestAsync<TCallbacks>(Uri url, string jsonContent, TCallbacks callbacks = default)
        where TCallbacks : struct, ICallbacks
    {
        var content = Encoding.UTF8.GetBytes(jsonContent);
        var result = await SendRequestAsync(url, content, callbacks).ConfigureAwait(false);
        return Encoding.UTF8.GetString(result);
    }

    private async Task<byte[]> SendRequestAsync<TCallbacks>(Uri uri, byte[] body, TCallbacks callbacks = default)
        where TCallbacks : struct, ICallbacks
    {
        return await WithRetries(
                       static async (state, finalTry) =>
                       {
                           var callbacks = state.Callbacks;
                           var sw = Stopwatch.StartNew();
                           try
                           {
                               callbacks.OnBeforeSend();
                               var client = state.Client;
                               var uri = state.Uri;
                               var body = state.Body;
#if NETFRAMEWORK
                               ConfigureServicePoint(uri);
#endif
                               var request = client._apiRequestFactory.Create(uri);
                               client.SetRequestHeader(request);

                               if (Log.IsEnabled(LogEventLevel.Debug))
                               {
                                   Log.Debug("TestOptimizationClient: Sending request to: {Url}", uri.ToString());
                               }

                               byte[]? responseContent;
                               try
                               {
                                   using var response = await request.PostAsync(new ArraySegment<byte>(body), MimeTypes.Json).ConfigureAwait(false);
                                   // TODO: Check for compressed responses - if we received one, currently these are not handled and would throw when we attempt to deserialize
                                   using var stream = await response.GetStreamAsync().ConfigureAwait(false);
                                   using var memoryStream = new MemoryStream();
                                   await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
                                   responseContent = memoryStream.ToArray();
                                   callbacks.OnStatusCodeReceived(response.StatusCode, responseContent.Length);
                                   client.CheckResponseStatusCode(response, responseContent, finalTry);
                                   Log.Debug<string, int>("TestOptimizationClient: Response received from: {Url} | {StatusCode}", uri.ToString(), response.StatusCode);
                               }
                               catch (Exception ex)
                               {
                                   Log.Error(ex, "TestOptimizationClient: Error getting result.");
#if NETFRAMEWORK
                                   CloseConnectionGroupOnFailure(uri, ex);
#endif
                                   callbacks.OnError(ex);
                                   throw;
                               }

                               return responseContent;
                           }
                           finally
                           {
                               callbacks.OnAfterSend(sw.Elapsed.TotalMilliseconds);
                           }
                       },
                       new UriAndBodyState<TCallbacks>(this, uri, body, callbacks),
                       MaxRetries)
                  .ConfigureAwait(false);
    }

    private async Task<byte[]> SendRequestAsync<TCallbacks>(Uri uri, MultipartFormItem[] items, TCallbacks callbacks = default)
        where TCallbacks : struct, ICallbacks
    {
        return await WithRetries(
                       static async (state, finalTry) =>
                       {
                           var callbacks = state.Callbacks;
                           var sw = Stopwatch.StartNew();
                           try
                           {
                               callbacks.OnBeforeSend();
                               var client = state.Client;
                               var uri = state.Uri;
                               var multipartFormItems = state.MultipartFormItems;
#if NETFRAMEWORK
                               ConfigureServicePoint(uri);
#endif
                               var request = client._apiRequestFactory.Create(uri);
                               client.SetRequestHeader(request);

                               if (Log.IsEnabled(LogEventLevel.Debug))
                               {
                                   Log.Debug("TestOptimizationClient: Sending request to: {Url}", uri.ToString());
                               }

                               byte[]? responseContent;
                               try
                               {
                                   using var response = await request.PostAsync(multipartFormItems).ConfigureAwait(false);
                                   // TODO: Check for compressed responses - if we received one, currently these are not handled and would throw when we attempt to deserialize
                                   using var stream = await response.GetStreamAsync().ConfigureAwait(false);
                                   using var memoryStream = new MemoryStream();
                                   await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
                                   responseContent = memoryStream.ToArray();
                                   callbacks.OnStatusCodeReceived(response.StatusCode, responseContent.Length);
                                   client.CheckResponseStatusCode(response, responseContent, finalTry);
                                   Log.Debug<string, int>("TestOptimizationClient: Response received from: {Url} | {StatusCode}", uri.ToString(), response.StatusCode);
                               }
                               catch (Exception ex)
                               {
                                   Log.Error(ex, "TestOptimizationClient: Error getting result.");
#if NETFRAMEWORK
                                   CloseConnectionGroupOnFailure(uri, ex);
#endif
                                   callbacks.OnError(ex);
                                   throw;
                               }

                               return responseContent;
                           }
                           finally
                           {
                               callbacks.OnAfterSend(sw.Elapsed.TotalMilliseconds);
                           }
                       },
                       new UriAndMultipartFormBodyState<TCallbacks>(this, uri, items, callbacks),
                       MaxRetries)
                  .ConfigureAwait(false);
    }

    private void SetRequestHeader(IApiRequest request)
    {
        request.AddHeader(HttpHeaderNames.TraceId, _id);
        request.AddHeader(HttpHeaderNames.ParentId, _id);
        if (_eventPlatformProxySupport is EventPlatformProxySupport.V2 or EventPlatformProxySupport.V4)
        {
            request.AddHeader(EvpSubdomainHeader, "api");
        }
        else
        {
            request.AddHeader(ApiKeyHeader, _testOptimization.Settings.ApiKey);
        }
    }

    private void CheckResponseStatusCode(IApiResponse response, byte[]? responseContent, bool finalTry)
    {
        // Check if the rate limit header was received.
        if (response.StatusCode == 429 &&
            response.GetHeader("x-ratelimit-reset") is { } strRateLimitDurationInSeconds &&
            int.TryParse(strRateLimitDurationInSeconds, out var rateLimitDurationInSeconds))
        {
            if (rateLimitDurationInSeconds > 30)
            {
                // If 'x-ratelimit-reset' is > 30 seconds we cancel the request.
                throw new RateLimitException();
            }

            throw new RateLimitException(rateLimitDurationInSeconds);
        }

        if (response.StatusCode is < 200 or >= 300 && response.StatusCode != 404 && response.StatusCode != 502)
        {
            var stringContent = responseContent != null ? Encoding.UTF8.GetString(responseContent) : string.Empty;
            if (finalTry)
            {
                Log.Error<int, string>("TestOptimizationClient: Request failed with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, stringContent);
            }

            throw new WebException($"Status: {response.StatusCode}, Content: {stringContent}");
        }
    }

    private async Task<T> WithRetries<T, TState>(Func<TState, bool, Task<T>> sendDelegate, TState state, int numOfRetries)
    {
        var retryCount = 1;
        var sleepDuration = 100; // in milliseconds

        while (true)
        {
            T response = default!;
            ExceptionDispatchInfo? exceptionDispatchInfo = null;
            var isFinalTry = retryCount >= numOfRetries;

            try
            {
                response = await sendDelegate(state, isFinalTry).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
            }

            // Error handling block
            if (exceptionDispatchInfo is not null)
            {
                var sourceException = exceptionDispatchInfo.SourceException;

                if (isFinalTry ||
                    sourceException is RateLimitException { DelayTimeInSeconds: null } ||
                    sourceException is DirectoryNotFoundException ||
                    sourceException is FileNotFoundException)
                {
                    // stop retrying
                    Log.Error<int>(sourceException, "TestOptimizationClient: An error occurred while sending data after {Retries} retries.", retryCount);
                    exceptionDispatchInfo.Throw();
                }

                // Before retry
                var isSocketException = false;
                var innerException = sourceException;
                while (innerException != null)
                {
                    if (innerException is SocketException)
                    {
                        isSocketException = true;
                        break;
                    }

                    innerException = innerException.InnerException;
                }

                if (isSocketException)
                {
                    Log.Debug(sourceException, "TestOptimizationClient: Unable to communicate with the server");
                }

                if (sourceException is RateLimitException { DelayTimeInSeconds: { } delayTimeInSeconds })
                {
                    // Execute rate limit retry delay
                    await Task.Delay(TimeSpan.FromSeconds(delayTimeInSeconds)).ConfigureAwait(false);
                }
                else
                {
                    // Execute retry delay
                    await Task.Delay(sleepDuration).ConfigureAwait(false);
                    sleepDuration *= 2;
                }

                retryCount++;
                continue;
            }

            Log.Debug("TestOptimizationClient: Request was completed.");
            return response;
        }
    }

#pragma warning disable SA1201
    internal interface ICallbacks
    {
        void OnBeforeSend();

        void OnStatusCodeReceived(int statusCode, int responseLength);

        void OnError(Exception ex);

        void OnAfterSend(double totalMs);
    }

    internal readonly struct ActionCallbacks : ICallbacks
    {
        private readonly Action? _onBeforeSend;
        private readonly Action<int, int>? _onStatusCodeReceived;
        private readonly Action<Exception>? _onError;
        private readonly Action<double>? _onAfterSend;

        public ActionCallbacks(Action? onBeforeSend = null, Action<int, int>? onStatusCodeReceived = null, Action<Exception>? onError = null, Action<double>? onAfterSend = null)
        {
            _onBeforeSend = onBeforeSend;
            _onStatusCodeReceived = onStatusCodeReceived;
            _onError = onError;
            _onAfterSend = onAfterSend;
        }

        public void OnBeforeSend() => _onBeforeSend?.Invoke();

        public void OnStatusCodeReceived(int statusCode, int responseLength) => _onStatusCodeReceived?.Invoke(statusCode, responseLength);

        public void OnError(Exception ex) => _onError?.Invoke(ex);

        public void OnAfterSend(double totalMs) => _onAfterSend?.Invoke(totalMs);
    }

    internal readonly struct NoopCallbacks : ICallbacks
    {
        public void OnBeforeSend()
        {
        }

        public void OnStatusCodeReceived(int statusCode, int responseLength)
        {
        }

        public void OnError(Exception ex)
        {
        }

        public void OnAfterSend(double totalMs)
        {
        }
    }

    private readonly struct UriAndBodyState<TCallbacks>
        where TCallbacks : struct, ICallbacks
    {
        public readonly TestOptimizationClient Client;
        public readonly Uri Uri;
        public readonly byte[] Body;
        public readonly TCallbacks Callbacks;

        public UriAndBodyState(TestOptimizationClient client, Uri uri, byte[] body, TCallbacks callbacks)
        {
            Client = client;
            Uri = uri;
            Body = body;
            Callbacks = callbacks;
        }
    }

    private readonly struct UriAndMultipartFormBodyState<TCallbacks>
        where TCallbacks : struct, ICallbacks
    {
        public readonly TestOptimizationClient Client;
        public readonly Uri Uri;
        public readonly MultipartFormItem[] MultipartFormItems;
        public readonly TCallbacks Callbacks;

        public UriAndMultipartFormBodyState(TestOptimizationClient client, Uri uri, MultipartFormItem[] multipartFormItems, TCallbacks callbacks)
        {
            Client = client;
            Uri = uri;
            MultipartFormItems = multipartFormItems;
            Callbacks = callbacks;
        }
    }

    private class RateLimitException : Exception
    {
        public RateLimitException()
            : base("Server rate limiting response received. Cancelling request.")
        {
            DelayTimeInSeconds = null;
        }

        public RateLimitException(int delayTimeInSeconds)
            : base($"Server rate limiting response received. Waiting for {delayTimeInSeconds} seconds")
        {
            DelayTimeInSeconds = delayTimeInSeconds;
        }

        public int? DelayTimeInSeconds { get; }
    }

#pragma warning disable SA1201
    private readonly struct DataEnvelope<T>
    {
        [JsonProperty("data")]
        public readonly T? Data;

        [JsonProperty("meta")]
        public readonly Metadata? Meta;

        public DataEnvelope(T? data, string? repositoryUrl)
        {
            Data = data;
            Meta = repositoryUrl is null ? null : new Metadata(repositoryUrl);
        }
    }

    private readonly struct DataArrayEnvelope<T>
    {
        [JsonProperty("data")]
        public readonly T[] Data;

        [JsonProperty("meta")]
        public readonly Metadata? Meta;

        public DataArrayEnvelope(T[] data, string? repositoryUrl)
        {
            Data = data;
            Meta = repositoryUrl is null ? null : new Metadata(repositoryUrl);
        }
    }

    private readonly struct Metadata
    {
        [JsonProperty("repository_url")]
        public readonly string RepositoryUrl;

        [JsonProperty("correlation_id")]
        public readonly string? CorrelationId;

        public Metadata(string repositoryUrl)
        {
            RepositoryUrl = repositoryUrl;
            CorrelationId = null;
        }

        public Metadata(string repositoryUrl, string? correlationId)
        {
            RepositoryUrl = repositoryUrl;
            CorrelationId = correlationId;
        }
    }

    private readonly struct Data<T>
    {
        [JsonProperty("id")]
        public readonly string? Id;

        [JsonProperty("type")]
        public readonly string Type;

        [JsonProperty("attributes")]
        public readonly T? Attributes;

        public Data(string? id, string type, T? attributes)
        {
            Id = id;
            Type = type;
            Attributes = attributes;
        }
    }
}
