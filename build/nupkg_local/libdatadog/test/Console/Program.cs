using System;
using System.Runtime.InteropServices;

class Program
{
    public static int Main(string[] args)
    {
        var url = new CharSlice("http://localhost:8126");
        var tracerVersion = new CharSlice("1.0.0");
        var language = new CharSlice(".NET");
        var languageVersion = new CharSlice("5.0.0");
        var languageInterpreter = new CharSlice(".NET");
        var hostname = new CharSlice("localhost");
        var env = new CharSlice("development");
        var service = new CharSlice("dotnet-test");
        var serviceVersion = new CharSlice("1.0.0");

        Console.WriteLine("Creating config");
        Native.ddog_trace_exporter_config_new(out var config);
        Console.WriteLine("Config created");

        Console.WriteLine("Setting config values");
        SetConfig(config, url, Native.ddog_trace_exporter_config_set_url);
        SetConfig(config, tracerVersion, Native.ddog_trace_exporter_config_set_tracer_version);
        SetConfig(config, tracerVersion, Native.ddog_trace_exporter_config_set_tracer_version);
        SetConfig(config, language, Native.ddog_trace_exporter_config_set_language);
        SetConfig(config, languageVersion, Native.ddog_trace_exporter_config_set_lang_version);
        SetConfig(config, languageInterpreter, Native.ddog_trace_exporter_config_set_lang_interpreter);
        SetConfig(config, hostname, Native.ddog_trace_exporter_config_set_hostname);
        SetConfig(config, env, Native.ddog_trace_exporter_config_set_env);
        SetConfig(config, service, Native.ddog_trace_exporter_config_set_service);
        SetConfig(config, serviceVersion, Native.ddog_trace_exporter_config_set_version);
        Console.WriteLine("Config values set");

        var newError = Native.ddog_trace_exporter_new(out var handle, config);
        if (newError != IntPtr.Zero)
        {
            Console.WriteLine("Error creating exporter");
            var error = GetError(newError);
            Console.WriteLine(error.Value.Code);
            Console.WriteLine(error.Value.ToString());
            Native.ddog_trace_exporter_config_free(config);
            return -1;
        }

        Console.WriteLine("Exporter created");

        Console.WriteLine("Sending traces");
        var traces = new byte[]
        {
            0x90
        };

        // pin traces
        var traceSlice = new ByteSlice
        {
            Ptr = Marshal.UnsafeAddrOfPinnedArrayElement(traces, 0),
            Len = (UIntPtr)traces.Length,
        };
        var traceCount = (UIntPtr)1;

        var responsePtr = IntPtr.Zero;
        var sendError = Native.ddog_trace_exporter_send(handle, traceSlice, traceCount, ref responsePtr);
        if (sendError != IntPtr.Zero)
        {
            Console.WriteLine("Error sending traces");
            var error = GetError(sendError);
            Console.WriteLine(error.Value.Code);
            Console.WriteLine(error.Value.ToString());
            Native.ddog_trace_exporter_error_free(sendError);

            // this is expected when agent is not running
            if (error.Value.Code != TraceExporterError.ErrorCode.IoError)
            {
                return -1;
            }
        }

        if (sendError == IntPtr.Zero)
        {
            // marshal the response struct
            var response = Marshal.PtrToStructure<AgentResponse>(responsePtr);
            Console.WriteLine("Response rate: " + response.Rate);
        }

        Console.WriteLine("Traces sent");

        Console.WriteLine("Freeing exporter");
        Native.ddog_trace_exporter_free(handle);
        Console.WriteLine("Exporter freed");
        Console.WriteLine("Done");
        return 0;
    }

    static TraceExporterError? GetError(IntPtr errorPtr)
        {
            if (errorPtr == IntPtr.Zero)
                return null;

            return Marshal.PtrToStructure<TraceExporterError>(errorPtr);
        }

        static void SetConfig(IntPtr config, CharSlice value, Func<IntPtr, CharSlice, IntPtr> setter)
        {
            Console.WriteLine("Setting config value for " + value);
            var error = setter(config, value);

            if (error == IntPtr.Zero)
            {
                return;
            }

            // marshal the error struct
            var errorStruct = Marshal.PtrToStructure<TraceExporterError>(error);
            Console.WriteLine("Error setting config value: " + errorStruct);
            Native.ddog_trace_exporter_error_free(error);
        }

    internal enum TraceExporterInputFormat
    {
        Proxy = 0,
        V04 = 1,
    }

    internal enum TraceExporterOutputFormat
    {
        V04 = 0,
        V07 = 1,
    }

    internal static class Native
    {
        private const string DllName = "datadog_profiling_ffi";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ddog_trace_exporter_error_free(IntPtr error);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ddog_trace_exporter_config_new(out IntPtr outHandle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ddog_trace_exporter_config_free(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ddog_trace_exporter_config_set_url(IntPtr config, CharSlice url);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ddog_trace_exporter_config_set_tracer_version(IntPtr config, CharSlice version);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ddog_trace_exporter_config_set_language(IntPtr config, CharSlice lang);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ddog_trace_exporter_config_set_lang_version(IntPtr config, CharSlice version);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ddog_trace_exporter_config_set_lang_interpreter(IntPtr config,
            CharSlice interpreter);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ddog_trace_exporter_config_set_hostname(IntPtr config, CharSlice hostname);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ddog_trace_exporter_config_set_env(IntPtr config, CharSlice env);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ddog_trace_exporter_config_set_version(IntPtr config, CharSlice version);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ddog_trace_exporter_config_set_service(IntPtr config, CharSlice service);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ddog_trace_exporter_new(out IntPtr outHandle, IntPtr config);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ddog_trace_exporter_free(IntPtr handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr ddog_trace_exporter_send(IntPtr handle, ByteSlice trace, UIntPtr traceCount,
            ref IntPtr response);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AgentResponse
    {
        internal double Rate;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ByteSlice
    {
        internal IntPtr Ptr;
        internal UIntPtr Len;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CharSlice
    {
        internal IntPtr Ptr;
        internal UIntPtr Len;

        internal CharSlice(string str)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(str);
            Ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, Ptr, bytes.Length);
            Len = (UIntPtr)bytes.Length;
        }
    }

/*
 * typedef struct ddog_TraceExporterError {
     enum ddog_TraceExporterErrorCode code;
     char *msg;
   } ddog_TraceExporterError;
 */
    [StructLayout(LayoutKind.Sequential)]
    internal struct TraceExporterError
    {
        internal ErrorCode Code;
        internal IntPtr Msg;

        internal enum ErrorCode
        {
            AddressInUse = 0,
            ConnectionAborted = 1,
            ConnectionRefused = 2,
            ConnectionReset = 3,
            HttpBodyFormat = 4,
            HttpBodyTooLong = 5,
            HttpClient = 6,
            HttpParse = 7,
            HttpServer = 8,
            HttpUnknown = 9,
            HttpWrongStatus = 10,
            InvalidArgument = 11,
            InvalidData = 12,
            InvalidInput = 13,
            InvalidUrl = 14,
            IoError = 15,
            NetworkUnknown = 16,
            Serde = 17,
            TimedOut = 18,
        }

        public override string ToString()
        {
            return IntPtrExtension.ToUtf8String(Msg);
        }
    }

    class IntPtrExtension
    {
        public static string ToUtf8String(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return string.Empty;
            }

            var len = 0;
            while (Marshal.ReadByte(ptr, len) != 0)
            {
                len++;
            }

            if (len == 0)
            {
                return string.Empty;
            }

            var buffer = new byte[len];
            Marshal.Copy(ptr, buffer, 0, buffer.Length);
            return System.Text.Encoding.UTF8.GetString(buffer);
        }
    }
}
