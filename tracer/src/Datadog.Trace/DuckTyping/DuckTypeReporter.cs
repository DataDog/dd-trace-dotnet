// <copyright file="DuckTypeReporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.DuckTyping;

internal static class DuckTypeReporter
{
    private const int MaxBufferBytes = 1 * 1024 * 1024;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DuckTypeReporter));
    private static readonly Uri Endpoint = ResolveEndpoint();
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);
    private static readonly HttpClient HttpClient = new();
    private static readonly object SyncRoot = new();
    private static readonly List<RecordPayload> Buffer = new();
    private static readonly Timer FlushTimer = new(_ => Task.Run(() => FlushInternalAsync(CancellationToken.None)), null, FlushInterval, FlushInterval);

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Include
    };

    private static readonly Task CompletedTask = Task.FromResult(0);

    private static int _bufferBytes;
    private static bool _flushInFlight;

    static DuckTypeReporter()
    {
        LifetimeManager.Instance.AddAsyncShutdownTask(_ => FlushAsync());
    }

    public static void ReportDuckType(Type proxyType, Type targetType)
    {
        if (proxyType == null)
        {
            throw new ArgumentNullException(nameof(proxyType));
        }

        if (targetType == null)
        {
            throw new ArgumentNullException(nameof(targetType));
        }

        var parentType = ResolveParentType(targetType);

        var proxyAssemblyQualifiedName = proxyType.AssemblyQualifiedName ?? proxyType.FullName ?? string.Empty;
        var targetAssemblyName = targetType.Assembly.GetName().Name ?? string.Empty;
        var targetTypeName = targetType.FullName ?? targetType.Name ?? string.Empty;

        var parentTargetAssemblyName = string.Empty;
        var parentTargetTypeName = string.Empty;
        if (parentType != null)
        {
            var parentAssemblyName = parentType.Assembly.GetName().Name;
            if (!string.IsNullOrEmpty(parentAssemblyName))
            {
                parentTargetAssemblyName = parentAssemblyName;
            }

            var parentFullName = parentType.FullName;
            if (!string.IsNullOrEmpty(parentFullName))
            {
                parentTargetTypeName = parentFullName;
            }
            else if (!string.IsNullOrEmpty(parentType.Name))
            {
                parentTargetTypeName = parentType.Name;
            }
        }

        var record = new RecordPayload
        {
            ProxyAssemblyQualifiedName = proxyAssemblyQualifiedName,
            TargetAssemblyName = targetAssemblyName,
            TargetTypeName = targetTypeName,
            ParentTargetAssemblyName = parentTargetAssemblyName,
            ParentTargetTypeName = parentTargetTypeName,
        };

        var recordBytes = EstimateSize(record);

        List<RecordPayload>? toFlush = null;

        lock (SyncRoot)
        {
            Buffer.Add(record);
            _bufferBytes += recordBytes;

            if (_bufferBytes >= MaxBufferBytes)
            {
                toFlush = DrainBuffer();
            }
        }

        if (toFlush != null)
        {
            _ = Task.Run(() => SendWithRetryAsync(toFlush, CancellationToken.None))
                    .ContinueWith(
                         t =>
                         {
                             if (t.IsFaulted)
                             {
                                 var exception = t.Exception?.Flatten();
                                 var message = exception is { InnerException: not null } ? exception.InnerException.Message : "unknown error";
                                 Log.Error(exception, "DuckTypeReporter flush failed: {0}", message);
                             }
                         },
                         TaskScheduler.Default);
        }
    }

    public static Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        List<RecordPayload>? toFlush;

        lock (SyncRoot)
        {
            toFlush = DrainBuffer();
            if (toFlush == null || toFlush.Count == 0)
            {
                return CompletedTask;
            }
        }

        return SendWithRetryAsync(toFlush, cancellationToken);
    }

    private static Task FlushInternalAsync(CancellationToken cancellationToken)
    {
        List<RecordPayload>? toFlush;

        lock (SyncRoot)
        {
            if (_flushInFlight)
            {
                return CompletedTask;
            }

            toFlush = DrainBuffer();
            if (toFlush == null || toFlush.Count == 0)
            {
                return CompletedTask;
            }

            _flushInFlight = true;
        }

        return SendWithRetryAsync(toFlush, cancellationToken)
           .ContinueWith(
                task =>
                {
                    lock (SyncRoot)
                    {
                        _flushInFlight = false;
                    }

                    if (task.IsFaulted)
                    {
                        var exception = task.Exception?.Flatten();
                        var message = exception is { InnerException: not null }
                                             ? exception.InnerException.Message
                                             : "unknown error";
                        Log.Error(exception, "DuckTypeReporter flush failed: {0}", message);
                    }
                },
                TaskScheduler.Default);
    }

    private static async Task SendWithRetryAsync(List<RecordPayload> payload, CancellationToken cancellationToken)
    {
        if (payload.Count == 0)
        {
            return;
        }

        const int maxAttempts = 5;
        var delay = TimeSpan.FromSeconds(1);
        var json = JsonConvert.SerializeObject(payload, SerializerSettings);
        var payloadBytes = Encoding.UTF8.GetByteCount(json);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
                    {
                        request.Content = content;

                        using (var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                return;
                            }

                            await HandleErrorAsync(response).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Error<int, string>("DuckTypeReporter attempt {0} failed: {1}", attempt, ex.Message);
                }
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }

        lock (SyncRoot)
        {
            Buffer.AddRange(payload);
            _bufferBytes += payloadBytes;
        }

        throw new InvalidOperationException("Failed to report duck type payload after multiple attempts.");
    }

    private static Uri ResolveEndpoint()
    {
        var endpoint = Environment.GetEnvironmentVariable("DUCKTYPE_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            return new Uri(endpoint);
        }

        var portValue = Environment.GetEnvironmentVariable("DUCKTYPE_PORT") ??
                        Environment.GetEnvironmentVariable("PORT");

        int port;
        if (!string.IsNullOrWhiteSpace(portValue) && int.TryParse(portValue, out port) && port > 0)
        {
            return new Uri($"http://localhost:{port}/records");
        }

        return new Uri("https://tough-badgers-kneel.loca.lt/records");
    }

    private static async Task HandleErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Log.Error<int, string>("DuckTypeReporter received {0}: {1}", (int)response.StatusCode, body);
    }

    private static List<RecordPayload>? DrainBuffer()
    {
        if (Buffer.Count == 0)
        {
            return null;
        }

        var toFlush = new List<RecordPayload>(Buffer);
        Buffer.Clear();
        _bufferBytes = 0;
        return toFlush;
    }

    private static int EstimateSize(RecordPayload record)
    {
        var total = 0;

        total += GetByteCount(record.ProxyAssemblyQualifiedName);
        total += GetByteCount(record.TargetAssemblyName);
        total += GetByteCount(record.TargetTypeName);
        total += GetByteCount(record.ParentTargetAssemblyName);
        total += GetByteCount(record.ParentTargetTypeName);

        // Rough overhead for JSON quotes, commas, braces, and property names
        total += 128;

        return total;
    }

    private static int GetByteCount(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        return Encoding.UTF8.GetByteCount(value);
    }

    private static Type? ResolveParentType(Type targetType)
    {
        if (targetType.BaseType != null && targetType.BaseType != typeof(object))
        {
            return targetType.BaseType;
        }

        var interfaces = targetType.GetInterfaces();
        return interfaces.Length > 0 ? interfaces[0] : null;
    }

    private sealed class RecordPayload
    {
        public string ProxyAssemblyQualifiedName { get; set; } = string.Empty;

        public string TargetAssemblyName { get; set; } = string.Empty;

        public string TargetTypeName { get; set; } = string.Empty;

        public string ParentTargetAssemblyName { get; set; } = string.Empty;

        public string ParentTargetTypeName { get; set; } = string.Empty;
    }
}
