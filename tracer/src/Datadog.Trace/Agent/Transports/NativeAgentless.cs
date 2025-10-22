// <copyright file="NativeAgentless.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;

namespace Datadog.Trace.Agent.Transports;

internal class NativeAgentless
{
    private const string LibraryName = "libagent"; // logical name used by DllImport

    // Callback implementations for async operations
    private static readonly Native.ResponseCallback ResponseCallback = OnResponse;
    private static readonly Native.ErrorCallback ErrorCallback = OnError;

    static NativeAgentless()
    {
        Environment.SetEnvironmentVariable("DD_LIBAGENT_PATH", "/Users/tony.redondo/libagent-macos-arm64/libagent.dylib");

#if NETCOREAPP3_0_OR_GREATER
        NativeLibrary.SetDllImportResolver(typeof(NativeAgentless).Assembly, Resolve);
        // NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, Resolve);
#else
        PreloadOnNetFx();
#endif
    }

#if NETCOREAPP3_0_OR_GREATER
    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        var env = Environment.GetEnvironmentVariable("DD_LIBAGENT_PATH");
        if (!string.IsNullOrEmpty(env) && NativeLibrary.TryLoad(env!, out var he))
        {
            return he;
        }

        return IntPtr.Zero; // fall back to default resolution
    }
#else
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpFileName);

    private static void PreloadOnNetFx()
    {
        var env = Environment.GetEnvironmentVariable("DD_LIBAGENT_PATH");
        if (!string.IsNullOrEmpty(env))
        {
            _ = LoadLibraryW(env!);
        }
    }
#endif

    private static void OnResponse(
        ushort status,
        IntPtr headersData,
        UIntPtr headersLen,
        IntPtr bodyData,
        UIntPtr bodyLen,
        IntPtr userData)
    {
        var context = (AsyncContext)GCHandle.FromIntPtr(userData).Target!;

        try
        {
            // Extract headers
            var headers = string.Empty;
            if (headersData != IntPtr.Zero && headersLen != UIntPtr.Zero)
            {
                var headerLen = (int)headersLen;
                var headerBytes = new byte[headerLen];
                Marshal.Copy(headersData, headerBytes, 0, headerLen);
                headers = System.Text.Encoding.UTF8.GetString(headerBytes);
            }

            // Extract body
            var body = Array.Empty<byte>();
            if (bodyData != IntPtr.Zero && bodyLen != UIntPtr.Zero)
            {
                var bodyLenInt = (int)bodyLen;
                body = new byte[bodyLenInt];
                Marshal.Copy(bodyData, body, 0, bodyLenInt);
            }

            var response = new Response(status, headers, body);
            context.TaskCompletionSource.SetResult(response);
        }
        catch (Exception ex)
        {
            context.Exception = ex;
            context.TaskCompletionSource.SetException(ex);
        }
    }

    private static void OnError(IntPtr errorMessage, IntPtr userData)
    {
        var context = (AsyncContext)GCHandle.FromIntPtr(userData).Target!;

        var error = errorMessage != IntPtr.Zero
                        ? Marshal.PtrToStringAnsi(errorMessage) ?? "Unknown error"
                        : "Unknown error";

        var exception = new LibAgentException(error, -1);
        context.TaskCompletionSource.SetException(exception);
    }

    public static void Initialize()
    {
        Native.Initialize();
    }

    public static void Shutdown()
    {
        Native.Stop();
    }

    // High-level async API
    public static async Task<Response> RequestAsync(
        string method,
        string path,
        string headers,
        byte[]? body = null,
        CancellationToken cancellationToken = default)
    {
        // Create context for this request
        var context = new AsyncContext();
        var handle = GCHandle.Alloc(context);

        try
        {
            // Prepare body data
            var bodyPtr = IntPtr.Zero;
            var bodyLen = UIntPtr.Zero;

            if (body is { Length: > 0 })
            {
                bodyPtr = Marshal.AllocHGlobal(body.Length);
                Marshal.Copy(body, 0, bodyPtr, body.Length);
                bodyLen = new UIntPtr((uint)body.Length);
            }

            // Run the FFI call on a background thread to make it truly async
            await Task.Run(
                () =>
            {
                try
                {
                    var result = Native.ProxyTraceAgent(
                        method,
                        path,
                        headers,
                        bodyPtr,
                        bodyLen,
                        ResponseCallback,
                        ErrorCallback,
                        GCHandle.ToIntPtr(handle));

                    // If the call returned an error code but no exception was set,
                    // create a generic exception
                    if (result != 0 && context.TaskCompletionSource.Task.Status != TaskStatus.Faulted)
                    {
                        var exception = new LibAgentException($"ProxyTraceAgent returned error code {result}", result);
                        context.TaskCompletionSource.SetException(exception);
                    }
                }
                catch (Exception ex)
                {
                    context.TaskCompletionSource.SetException(ex);
                }
                finally
                {
                    // Clean up allocated memory
                    if (bodyPtr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(bodyPtr);
                    }
                }
            },
                cancellationToken).ConfigureAwait(false);

            return await context.TaskCompletionSource.Task.ConfigureAwait(false);
        }
        finally
        {
            handle.Free();
        }
    }

    // Convenience methods
    public static Task<Response> GetAsync(string path, NameValueCollection? headers = null, CancellationToken cancellationToken = default)
        => RequestAsync("GET", path, SerializeHttpHeaders(headers), null, cancellationToken);

    public static Task<Response> PostAsync(string path, NameValueCollection? headers = null, byte[]? body = null, CancellationToken cancellationToken = default)
        => RequestAsync("POST", path, SerializeHttpHeaders(headers), body, cancellationToken);

    public static Task<Response> PutAsync(string path, NameValueCollection? headers = null, byte[]? body = null, CancellationToken cancellationToken = default)
        => RequestAsync("PUT", path, SerializeHttpHeaders(headers), body, cancellationToken);

    public static Task<Response> DeleteAsync(string path, NameValueCollection? headers = null, CancellationToken cancellationToken = default)
        => RequestAsync("DELETE", path, SerializeHttpHeaders(headers), body: null, cancellationToken);

    public static bool SendMetric(string metric)
    {
        var metricBytes = ArrayPool<byte>.Shared.Rent(metric.Length * 2);
        try
        {
            var metricBytesLength = Encoding.UTF8.GetBytes(metric, 0, metric.Length, metricBytes, 0);
            return Native.SendDogStatsDMetric(metricBytes, (UIntPtr)metricBytesLength) == 0;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(metricBytes);
        }
    }

    private static NameValueCollection ParseHttpHeaders(string headerString)
    {
        if (string.IsNullOrWhiteSpace(headerString))
        {
            return new NameValueCollection();
        }

        var headers = new NameValueCollection();
        var lines = headerString.Split(["\r\n", "\n"], StringSplitOptions.None);

        string? currentKey = null;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                // Skip empty lines
                continue;
            }

            if (line.StartsWith(" ") || line.StartsWith("\t"))
            {
                // Continuation of previous header's value
                if (currentKey != null)
                {
                    headers[currentKey] += " " + line.Trim();
                }
            }
            else
            {
                // New header: find the colon
                var colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = line.Substring(0, colonIndex).Trim();
                    var value = line.Substring(colonIndex + 1).TrimStart(); // Trim only leading spaces
                    key = key.ToLowerInvariant(); // Headers are case-insensitive
                    headers.Add(key, value);
                    currentKey = key;
                }
            }
        }

        return headers;
    }

    private static string SerializeHttpHeaders(NameValueCollection? headers)
    {
        if (headers == null)
        {
            return string.Empty;
        }

        var sb = StringBuilderCache.Acquire();
        foreach (var key in headers.AllKeys)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            var titleCaseKey = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(key.ToLower());
            // Handle multiple values by joining with comma
            var values = headers.GetValues(key) ?? [];
            var joinedValues = string.Join(", ", values);
            sb.AppendLine($"{titleCaseKey}: {joinedValues}");
        }

        return StringBuilderCache.GetStringAndRelease(sb);
    }

    private static class Native
    {
        // Callback delegates
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ResponseCallback(
            ushort status,
            IntPtr headersData,
            UIntPtr headersLen,
            IntPtr bodyData,
            UIntPtr bodyLen,
            IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ErrorCallback(IntPtr errorMessage, IntPtr userData);

        [DllImport("libagent")]
        public static extern int ProxyTraceAgent(
            string method,
            string path,
            string headers,
            IntPtr bodyPtr,
            UIntPtr bodyLen,
            ResponseCallback onResponse,
            ErrorCallback onError,
            IntPtr userData);

        [DllImport("libagent")]
        public static extern int SendDogStatsDMetric(byte[] payload, UIntPtr payloadLen);

        [DllImport("libagent")]
        public static extern void Initialize();

        [DllImport("libagent", EntryPoint = "GetMetrics")]
        public static extern MetricsData GetMetrics();

        [DllImport("libagent")]
        public static extern void Stop();

        [StructLayout(LayoutKind.Sequential)]
        public struct MetricsData
        {
            public ulong AgentSpawns;
            public ulong TraceAgentSpawns;
            public ulong AgentFailures;
            public ulong TraceAgentFailures;
            public double UptimeSeconds;

            public ulong ProxyGetRequests;
            public ulong ProxyPostRequests;
            public ulong ProxyPutRequests;
            public ulong ProxyDeleteRequests;
            public ulong ProxyPatchRequests;
            public ulong ProxyHeadRequests;
            public ulong ProxyOptionsRequests;
            public ulong ProxyOtherRequests;

            public ulong Proxy2xxResponses;
            public ulong Proxy3xxResponses;
            public ulong Proxy4xxResponses;
            public ulong Proxy5xxResponses;

            public double ResponseTimeEmaAll;
            public double ResponseTimeEma2xx;
            public double ResponseTimeEma4xx;
            public double ResponseTimeEma5xx;
            public ulong ResponseTimeSampleCount;

            public ulong DogStatsdRequests;
            public ulong DogstatsdSuccesses;
            public ulong DogStatsdErrors;
        }
    }

    // Response data structure
    public class Response
    {
        public Response(ushort status, string headers, byte[] body)
        {
            Status = status;
            Headers = ParseHttpHeaders(headers);
            Body = body;
        }

        public ushort Status { get; }

        public NameValueCollection Headers { get; }

        public byte[] Body { get; }
    }

    // Exception for API errors
    public class LibAgentException : Exception
    {
        public LibAgentException(string message, int errorCode)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public int ErrorCode { get; }
    }

    // Context for async operations
    private class AsyncContext
    {
        public TaskCompletionSource<Response> TaskCompletionSource { get; } = new();

        public Exception? Exception { get; set; }
    }
}
