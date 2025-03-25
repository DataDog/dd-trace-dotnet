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

    internal static CharSlice CreateCharSlice(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return new CharSlice { Handle = IntPtr.Zero, Length = UIntPtr.Zero };
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return new CharSlice { Handle = ptr, Length = new UIntPtr((uint)bytes.Length) };
    }

    internal static void FreeCharSlice(CharSlice slice)
    {
        if (slice.Handle != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(slice.Handle);
        }
    }

    public static TracerMemfdHandle StoreTracerMetadata(
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

            if (result.IsError)
            {
                throw new Exception($"Failed to store tracer metadata: {result.ErrorMsg}");
            }

            return result.Value;
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

    public static void CloseTracerMemfdHandle(TracerMemfdHandle handle)
    {
        NativeInterop.Ddcommon.CloseTracerMemfd(handle);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TracerMemfdHandle
    {
        public int FileHandle;
        public bool IsValid;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CharSlice
    {
        public IntPtr Handle;
        public UIntPtr Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MaybeError<T>
        where T : struct
    {
        public T Value;
        public bool IsError;

        [MarshalAs(UnmanagedType.LPStr)]
        public string ErrorMsg;
    }
}
