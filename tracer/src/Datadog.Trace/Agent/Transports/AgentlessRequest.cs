// <copyright file="AgentlessRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if NETCOREAPP
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports;

internal class AgentlessRequest : IApiRequest
{
    private const string Version = "v0.4";
    private const string Endpoint = "/v0.4/traces";
    private readonly Dictionary<string, string> _headers = new();
    private readonly string _endpoint;

    static AgentlessRequest()
    {
        TraceAgent.Initialize();
    }

    public AgentlessRequest(Uri endpoint)
    {
        _endpoint = endpoint.ToString();
    }

    public void AddHeader(string name, string value)
    {
        _headers.Add(name, value);
    }

    public Task<IApiResponse> GetAsync()
    {
        var ms = new MemoryStream();
        ms.Write("{}"u8);
        ms.Position = 0;
        return Task.FromResult((IApiResponse)new AgentlessResponse(200, _headers, ms));
    }

    public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType)
    {
        if (_endpoint == Endpoint)
        {
            _headers["Content-Type"] = contentType;
            Console.WriteLine("Headers: {0}", _headers.Count);
            Console.WriteLine("Data: {0}", bytes.Count);
            return Task.FromResult(TraceAgent.SubmitTraces(Version, _headers, bytes) ?
                                       (IApiResponse)new AgentlessResponse(200, _headers, Stream.Null) :
                                       (IApiResponse)new AgentlessResponse(500, _headers, Stream.Null));
        }

        return Task.FromResult((IApiResponse)new AgentlessResponse(500, _headers, Stream.Null));
    }

    public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
    {
        if (_endpoint == Endpoint)
        {
            _headers["Content-Type"] = contentType;
            _headers["Content-Encoding"] = contentEncoding;
            Console.WriteLine("Headers: {0}", _headers.Count);
            Console.WriteLine("Data: {0}", bytes.Count);
            return Task.FromResult(TraceAgent.SubmitTraces(Version, _headers, bytes) ?
                                       (IApiResponse)new AgentlessResponse(200, _headers, Stream.Null) :
                                       (IApiResponse)new AgentlessResponse(500, _headers, Stream.Null));
        }

        return Task.FromResult((IApiResponse)new AgentlessResponse(500, _headers, Stream.Null));
    }

    public Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary)
    {
        throw new NotImplementedException();
    }

    public Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// High-level, safe wrapper around the native trace-agent FFI.
    /// </summary>
    public static class TraceAgent
    {
        /// <summary>Initialize the agent. Returns true on success.</summary>
        public static bool Initialize() => Native.Initialize();

        /// <summary>Stop the agent. Returns true on success.</summary>
        public static bool Stop() => Native.Stop();

        /// <summary>
        /// Submit traces to the agent.
        /// </summary>
        /// <param name="version">Agent or client version (UTF-8 marshaled).</param>
        /// <param name="headers">String headers (e.g., tags/metadata). Keys/values are UTF-8 marshaled.</param>
        /// <param name="payload">Raw serialized trace bytes.</param>
        public static bool SubmitTraces(
            string version,
            IReadOnlyDictionary<string, string> headers,
            ReadOnlySpan<byte> payload)
        {
            if (version is null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (headers is null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            // We will build unmanaged copies of:
            // - version (char*)
            // - an array of key_value_pair structs with (char* key, char* value)
            // - payload bytes
            var rentals = new UnmanagedList();

            try
            {
                // version -> char* (UTF-8)
                var versionPtr = rentals.AllocUtf8(version);

                // headers -> key_value_pair_array
                Native.KeyValuePairArray headersArray = default;
                if (headers.Count > 0)
                {
                    // allocate array of key_value_pair
                    var count = headers.Count;
                    var elemSize = Marshal.SizeOf<Native.KeyValuePair>();
                    var arrayPtr = rentals.Alloc(elemSize * count, zeroInit: true);

                    var i = 0;
                    foreach (var kv in headers)
                    {
                        // Allocate UTF-8 copies for key/value
                        var keyPtr = rentals.AllocUtf8(kv.Key);
                        var valPtr = rentals.AllocUtf8(kv.Value);

                        // Write the struct elements into the unmanaged array
                        var pair = new Native.KeyValuePair { Key = keyPtr, Value = valPtr };
                        var dest = arrayPtr + (i * elemSize);
                        Marshal.StructureToPtr(pair, dest, fDeleteOld: false);
                        i++;
                    }

                    headersArray = new Native.KeyValuePairArray
                    {
                        Data = arrayPtr,
                        Length = (UIntPtr)headers.Count
                    };
                }

                // payload -> byte_array
                Native.ByteArray payloadArray = default;
                if (!payload.IsEmpty)
                {
                    var payloadPtr = rentals.Alloc(payload.Length, zeroInit: false);
                    Marshal.Copy(payload.ToArray(), 0, payloadPtr, payload.Length); // small copy; if you want to avoid ToArray, pin and copy via unsafe
                    payloadArray = new Native.ByteArray
                    {
                        Data = payloadPtr,
                        Length = (UIntPtr)payload.Length
                    };
                }

                // call into native
                var tp = new Native.TracesPayload
                {
                    Version = versionPtr,
                    Headers = headersArray,
                    Body = payloadArray
                };

                return Native.SubmitTraces(tp);
            }
            finally
            {
                rentals.Dispose();
            }
        }

        /// <summary>
        /// Tracks unmanaged allocations to guarantee cleanup even on exception paths.
        /// </summary>
        private sealed class UnmanagedList : IDisposable
        {
            private readonly List<IntPtr> _allocs = new();

            public IntPtr AllocUtf8(string s)
            {
#if NET5_0_OR_GREATER
                // Marshal.StringToCoTaskMemUTF8 is available and correct
                var ptr = Marshal.StringToCoTaskMemUTF8(s);
                _allocs.Add(ptr);
                return ptr;
#else
                var bytes = System.Text.Encoding.UTF8.GetBytes(s + "\0");
                var ptr = Alloc(bytes.Length, zeroInit: false);
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                return ptr;
#endif
            }

            public unsafe IntPtr Alloc(int sizeBytes, bool zeroInit)
            {
                if (sizeBytes <= 0)
                {
                    // Still allocate 1 byte to preserve distinct non-null pointer where caller expects one.
                    sizeBytes = 1;
                }

                var ptr = Marshal.AllocHGlobal(sizeBytes);
                _allocs.Add(ptr);
                if (zeroInit)
                {
                    Span<byte> span = new(ptr.ToPointer(), sizeBytes);
                    span.Clear();
                }

                return ptr;
            }

            public void Dispose()
            {
                // Free in reverse order for good measure
                for (var i = _allocs.Count - 1; i >= 0; i--)
                {
                    if (_allocs[i] != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(_allocs[i]);
                    }
                }

                _allocs.Clear();
            }
        }

#pragma warning disable SA1204
        private static class Native
        {
            private const string LogicalName = "trace-agent";

            static Native()
            {
                // Resolver multiplataforma del nombre lógico → archivo real
                NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, (name, asm, path) =>
                {
                    if (!string.Equals(name, LogicalName, StringComparison.Ordinal))
                    {
                        return IntPtr.Zero;
                    }

                    var candidates =
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new[] { "trace-agent.dll" } :
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? new[] { "libtrace-agent.dylib", "trace-agent.dylib" } :
                        new[] { "libtrace-agent.so", "trace-agent.so" };

                    foreach (var cand in candidates)
                    {
                        if (NativeLibrary.TryLoad(cand, asm, path, out var h))
                        {
                            return h;
                        }

                        try
                        {
                            return NativeLibrary.Load(cand);
                        }
                        catch
                        {
                            // ...
                        }
                    }

                    return IntPtr.Zero;
                });
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct KeyValuePair
            {
                public IntPtr Key;
                public IntPtr Value;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct KeyValuePairArray
            {
                public IntPtr Data;
                public UIntPtr Length;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct ByteArray
            {
                public IntPtr Data;
                public UIntPtr Length;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct TracesPayload
            {
                public IntPtr Version;
                public KeyValuePairArray Headers;
                public ByteArray Body;
            }

#pragma warning disable SA1201

            [DllImport(LogicalName, EntryPoint = "initialize", CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool Initialize();

            [DllImport(LogicalName, EntryPoint = "stop", CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool Stop();

            [DllImport(LogicalName, EntryPoint = "submit_traces", CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool SubmitTraces(TracesPayload payload);
        }
    }
}
#endif
