// <copyright file="AgentlessRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

#if NETCOREAPP
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports;

internal class AgentlessRequest : IApiRequest
{
    private const string Version = "v0.4";
    private const string TracesEndpoint = "/v0.4/traces";
    private const string ConfigEndpoint = "v0.7/config";
    private readonly Dictionary<string, string> _headers = new();
    private readonly string _endpoint;

    private readonly string _infoBytes = "\n{\n\t\"version\": \"7.69.4\",\n\t\"git_commit\": \"9bd1f3ecfc\",\n\t\"endpoints\": [\n\t\t\"/v0.3/traces\",\n\t\t\"/v0.3/services\",\n\t\t\"/v0.4/traces\",\n\t\t\"/v0.4/services\",\n\t\t\"/v0.5/traces\",\n\t\t\"/v0.7/traces\",\n\t\t\"/profiling/v1/input\",\n\t\t\"/telemetry/proxy/\",\n\t\t\"/v0.6/stats\",\n\t\t\"/v0.1/pipeline_stats\",\n\t\t\"/openlineage/api/v1/lineage\",\n\t\t\"/evp_proxy/v1/\",\n\t\t\"/evp_proxy/v2/\",\n\t\t\"/evp_proxy/v3/\",\n\t\t\"/evp_proxy/v4/\",\n\t\t\"/debugger/v1/input\",\n\t\t\"/debugger/v1/diagnostics\",\n\t\t\"/symdb/v1/input\",\n\t\t\"/dogstatsd/v1/proxy\",\n\t\t\"/dogstatsd/v2/proxy\",\n\t\t\"/tracer_flare/v1\",\n\t\t\"/v0.7/config\",\n\t\t\"/config/set\"\n\t],\n\t\"client_drop_p0s\": true,\n\t\"span_meta_structs\": true,\n\t\"long_running_spans\": true,\n\t\"span_events\": true,\n\t\"evp_proxy_allowed_headers\": [\n\t\t\"Content-Type\",\n\t\t\"Accept-Encoding\",\n\t\t\"Content-Encoding\",\n\t\t\"User-Agent\",\n\t\t\"DD-CI-PROVIDER-NAME\"\n\t],\n\t\"config\": {\n\t\t\"default_env\": \"none\",\n\t\t\"target_tps\": 10,\n\t\t\"max_eps\": 200,\n\t\t\"receiver_port\": 8126,\n\t\t\"receiver_socket\": \"\",\n\t\t\"connection_limit\": 0,\n\t\t\"receiver_timeout\": 0,\n\t\t\"max_request_bytes\": 26214400,\n\t\t\"statsd_port\": 8135,\n\t\t\"max_memory\": 500000000,\n\t\t\"max_cpu\": 0.5,\n\t\t\"analyzed_spans_by_service\": {},\n\t\t\"obfuscation\": {\n\t\t\t\"elastic_search\": true,\n\t\t\t\"mongo\": true,\n\t\t\t\"sql_exec_plan\": false,\n\t\t\t\"sql_exec_plan_normalize\": false,\n\t\t\t\"http\": {\n\t\t\t\t\"remove_query_string\": false,\n\t\t\t\t\"remove_path_digits\": false\n\t\t\t},\n\t\t\t\"remove_stack_traces\": false,\n\t\t\t\"redis\": {\n\t\t\t\t\"Enabled\": true,\n\t\t\t\t\"RemoveAllArgs\": false\n\t\t\t},\n\t\t\t\"valkey\": {\n\t\t\t\t\"Enabled\": true,\n\t\t\t\t\"RemoveAllArgs\": false\n\t\t\t},\n\t\t\t\"memcached\": {\n\t\t\t\t\"Enabled\": true,\n\t\t\t\t\"KeepCommand\": false\n\t\t\t}\n\t\t}\n\t},\n\t\"peer_tags\": [\n\t\t\"_dd.base_service\",\n\t\t\"active_record.db.vendor\",\n\t\t\"amqp.destination\",\n\t\t\"amqp.exchange\",\n\t\t\"amqp.queue\",\n\t\t\"aws.queue.name\",\n\t\t\"aws.s3.bucket\",\n\t\t\"bucketname\",\n\t\t\"cassandra.keyspace\",\n\t\t\"db.cassandra.contact.points\",\n\t\t\"db.couchbase.seed.nodes\",\n\t\t\"db.hostname\",\n\t\t\"db.instance\",\n\t\t\"db.name\",\n\t\t\"db.namespace\",\n\t\t\"db.system\",\n\t\t\"db.type\",\n\t\t\"dns.hostname\",\n\t\t\"grpc.host\",\n\t\t\"hostname\",\n\t\t\"http.host\",\n\t\t\"http.server_name\",\n\t\t\"messaging.destination\",\n\t\t\"messaging.destination.name\",\n\t\t\"messaging.kafka.bootstrap.servers\",\n\t\t\"messaging.rabbitmq.exchange\",\n\t\t\"messaging.system\",\n\t\t\"mongodb.db\",\n\t\t\"msmq.queue.path\",\n\t\t\"net.peer.name\",\n\t\t\"network.destination.ip\",\n\t\t\"network.destination.name\",\n\t\t\"out.host\",\n\t\t\"peer.hostname\",\n\t\t\"peer.service\",\n\t\t\"queuename\",\n\t\t\"rpc.service\",\n\t\t\"rpc.system\",\n\t\t\"sequel.db.vendor\",\n\t\t\"server.address\",\n\t\t\"streamname\",\n\t\t\"tablename\",\n\t\t\"topicname\"\n\t],\n\t\"span_kinds_stats_computed\": [\n\t\t\"server\",\n\t\t\"consumer\",\n\t\t\"client\",\n\t\t\"producer\"\n\t],\n\t\"obfuscation_version\": 1\n}";

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
        Console.WriteLine(@"****** GET {0}", _endpoint);
        if (_endpoint is "/info" or "info")
        {
            return Task.FromResult<IApiResponse>(new AgentlessResponse(200, _headers, new MemoryStream(Encoding.UTF8.GetBytes(_infoBytes))));
        }

        var ms = new MemoryStream();
        ms.Write("{}"u8);
        ms.Position = 0;
        return Task.FromResult<IApiResponse>(new AgentlessResponse(200, _headers, ms));
    }

    public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType)
    {
        Console.WriteLine(@"****** POST {0}", _endpoint);

        if (_endpoint == TracesEndpoint)
        {
            _headers["Content-Type"] = contentType;
            Console.WriteLine(@"Headers: {0}", _headers.Count);
            Console.WriteLine(@"Data: {0}", bytes.Count);
            return Task.FromResult<IApiResponse>(TraceAgent.SubmitTraces(Version, _headers, bytes) ?
                                                    new AgentlessResponse(200, _headers, Stream.Null) :
                                                    new AgentlessResponse(500, _headers, Stream.Null));
        }

        if (_endpoint == ConfigEndpoint)
        {
            _headers["Content-Type"] = contentType;
            Console.WriteLine(@"Headers: {0}", _headers.Count);
            Console.WriteLine(@"Data: {0}", bytes.Count);
            var response = TraceAgent.SubmitConfig(_headers, bytes);
            return response.Length == 0 ?
                       Task.FromResult<IApiResponse>(new AgentlessResponse(500, _headers, Stream.Null)) :
                       Task.FromResult<IApiResponse>(new AgentlessResponse(200, _headers, new MemoryStream(response)));
        }

        return Task.FromResult((IApiResponse)new AgentlessResponse(500, _headers, Stream.Null));
    }

    public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
    {
        Console.WriteLine(@"****** POST {0}", _endpoint);

        if (_endpoint == TracesEndpoint)
        {
            _headers["Content-Type"] = contentType;
            _headers["Content-Encoding"] = contentEncoding;
            Console.WriteLine(@"Headers: {0}", _headers.Count);
            Console.WriteLine(@"Data: {0}", bytes.Count);
            return Task.FromResult<IApiResponse>(TraceAgent.SubmitTraces(Version, _headers, bytes) ?
                                                     new AgentlessResponse(200, _headers, Stream.Null) :
                                                     new AgentlessResponse(500, _headers, Stream.Null));
        }

        if (_endpoint == ConfigEndpoint)
        {
            _headers["Content-Type"] = contentType;
            _headers["Content-Encoding"] = contentEncoding;
            Console.WriteLine(@"Headers: {0}", _headers.Count);
            Console.WriteLine(@"Data: {0}", bytes.Count);
            var response = TraceAgent.SubmitConfig(_headers, bytes);
            return response.Length == 0 ?
                       Task.FromResult<IApiResponse>(new AgentlessResponse(500, _headers, Stream.Null)) :
                       Task.FromResult<IApiResponse>(new AgentlessResponse(200, _headers, new MemoryStream(response)));
        }

        return Task.FromResult((IApiResponse)new AgentlessResponse(500, _headers, Stream.Null));
    }

    public Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary)
    {
        Console.WriteLine(@"****** POST {0}", _endpoint);
        throw new NotImplementedException();
    }

    public Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
    {
        Console.WriteLine(@"****** POST {0}", _endpoint);
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
        public static unsafe bool SubmitTraces(
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
                    var payloadPtrSpan = new Span<byte>((void*)payloadPtr, payload.Length);
                    payload.CopyTo(payloadPtrSpan);
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

        public static unsafe byte[] SubmitConfig(IReadOnlyDictionary<string, string> headers, ReadOnlySpan<byte> payload)
        {
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
                    var payloadPtrSpan = new Span<byte>((void*)payloadPtr, payload.Length);
                    payload.CopyTo(payloadPtrSpan);
                    payloadArray = new Native.ByteArray
                    {
                        Data = payloadPtr,
                        Length = (UIntPtr)payload.Length
                    };
                }

                // call into native
                var tp = new Native.TracesPayload
                {
                    Version = IntPtr.Zero,
                    Headers = headersArray,
                    Body = payloadArray
                };

                var response = Native.SubmitConfig(tp);
                var responseBytes = new byte[(int)response.Body.Length];
                var responsePtrSpan = new Span<byte>((void*)response.Body.Data, responseBytes.Length);
                responsePtrSpan.CopyTo(responseBytes);
                return responseBytes;
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

            [DllImport(LogicalName, EntryPoint = "submit_config", CallingConvention = CallingConvention.Cdecl)]
            internal static extern TracesPayload SubmitConfig(TracesPayload payload);
        }
    }
}
#endif
