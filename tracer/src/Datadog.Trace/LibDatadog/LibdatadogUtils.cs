// <copyright file="LibdatadogUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;

namespace Datadog.Trace.LibDatadog;

internal class LibdatadogUtils
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LibdatadogUtils>();

    public enum ResultTag
    {
        Ok,
        Err
    }

    internal static CharSlice CreateCharSlice(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return new CharSlice { Handle = IntPtr.Zero, Length = UIntPtr.Zero };
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return new CharSlice { Handle = ptr, Length = new UIntPtr((uint)str.Length) };
    }

    internal static void FreeCharSlice(CharSlice slice)
    {
        if (slice.Handle != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(slice.Handle);
        }
    }

    public static TracerMemfdHandleResult StoreTracerMetadata(
        byte schemaVersion,
        string runtimeId,
        string tracerLanguage,
        string tracerVersion,
        string hostname,
        string serviceName,
        string serviceEnv,
        string serviceVersion)
    {
        Log.Information("calling StoreTracerMetadata");
        var runtimeIdSlice = CreateCharSlice(runtimeId);
        var tracerLanguageSlice = CreateCharSlice(tracerLanguage);
        var tracerVersionSlice = CreateCharSlice(tracerVersion);
        var hostnameSlice = CreateCharSlice(hostname);
        var serviceNameSlice = CreateCharSlice(serviceName);
        var serviceEnvSlice = CreateCharSlice(serviceEnv);
        var serviceVersionSlice = CreateCharSlice(serviceVersion);

        try
        {
            var result = NativeInterop.Ddcommon.OpenTracerMemfd(
                schemaVersion,
                runtimeIdSlice,
                tracerLanguageSlice,
                tracerVersionSlice,
                hostnameSlice,
                serviceNameSlice,
                serviceEnvSlice,
                serviceVersionSlice);

            return result;
        }
        finally
        {
            FreeCharSlice(runtimeIdSlice);
            FreeCharSlice(tracerLanguageSlice);
            FreeCharSlice(tracerVersionSlice);
            FreeCharSlice(hostnameSlice);
            FreeCharSlice(serviceNameSlice);
            FreeCharSlice(serviceEnvSlice);
            FreeCharSlice(serviceVersionSlice);
            Log.Information("finished calling StoreTracerMetadata");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TracerMemfdHandle
    {
        public int FileHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CharSlice
    {
        public IntPtr Handle;
        public UIntPtr Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Error
    {
        private CharSlice message;

        public string Message()
        {
            if (message.Handle == IntPtr.Zero)
            {
                return string.Empty;
            }

            var bytes = new byte[message.Length.ToUInt32()];
            Marshal.Copy(message.Handle, bytes, 0, bytes.Length);
            var errorMessage = System.Text.Encoding.UTF8.GetString(bytes);
            // NativeInterop.Ddcommon.DropCharSlice(ref message);
            return errorMessage;
        }

        // Clean up the error using the FFI function
        public void Dispose()
        {
            // NativeMethods.ddog_Error_drop(ref this);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct TracerMemfdHandleResult
    {
        [FieldOffset(0)]
        public ResultTag Tag;

        [FieldOffset(8)] // Ensure proper alignment
        public TracerMemfdHandle Ok;

        [FieldOffset(8)]
        public Error Err;
    }
}
