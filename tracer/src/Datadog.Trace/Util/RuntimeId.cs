// <copyright file="RuntimeId.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util
{
    internal static class RuntimeId
    {
        private const string NativeLoaderBaseFilename = "Datadog.AutoInstrumentation.NativeLoader";

#if NETFRAMEWORK
        private const string NativeLoaderLibName = "Datadog.AutoInstrumentation.NativeLoader.dll";
#else
        private const string NativeLoaderLibName = "Datadog.AutoInstrumentation.NativeLoader";
#endif

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RuntimeId));

        public static string Get()
        {
            if (TryGetRuntimeIdFromNative(out var runtimeId))
            {
                Log.Information("Runtime id retrieved from native: " + runtimeId);
                return runtimeId;
            }

            var guid = Guid.NewGuid().ToString();
            Log.Information("Unable to get the runtime id from native. Fallback to Guid.NewGuid() : " + guid);

            return guid;
        }

        [DllImport(NativeLoaderLibName, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetCurrentAppDomainRuntimeId")]
        private static extern IntPtr GetRuntimeIdFromNative();

        private static bool TryGetRuntimeIdFromNative(out string runtimeId)
        {
            if (!IsNativeLoaderPresent())
            {
                runtimeId = default;
                return false;
            }

            try
            {
                var runtimeIdPtr = GetRuntimeIdFromNative();
                runtimeId = Marshal.PtrToStringAnsi(runtimeIdPtr);
                return !string.IsNullOrWhiteSpace(runtimeId);
            }
            catch (Exception e)
            {
                Log.Information(e, "An exception occured while retrieving the runtime-id from native.");
            }

            runtimeId = default;
            return false;
        }

        private static bool IsNativeLoaderPresent()
        {
            var frameworkDescription = FrameworkDescription.Create();
            var loaderFilename = GetNativeLoaderFilename(frameworkDescription);

            if (string.IsNullOrWhiteSpace(loaderFilename))
            {
                return false;
            }

            var profilerPath = GetProfilerPath(frameworkDescription);
            return !string.IsNullOrWhiteSpace(profilerPath) && Path.GetFileName(profilerPath).EndsWith(loaderFilename);
        }

        private static string GetProfilerPath(FrameworkDescription frameworkDescription)
        {
            var profilerEnvVar =
                frameworkDescription.IsCoreClr() ? "CORECLR_PROFILER_PATH" : "COR_PROFILER_PATH";
            var profilerEnvVarBitsExt =
                profilerEnvVar + (Environment.Is64BitProcess ? "_64" : "_32");

            var profilerPathsEnvVars = new List<string>()
            {
                profilerEnvVarBitsExt, profilerEnvVar
            };

            var profilerFolders =
                profilerPathsEnvVars
                   .Select(Environment.GetEnvironmentVariable)
                   .Where(x => !string.IsNullOrWhiteSpace(x))
                   .FirstOrDefault();

            return profilerFolders;
        }

        private static string GetNativeLoaderFilename(FrameworkDescription frameworkDescription)
        {
            var extension = frameworkDescription.OSPlatform switch
            {
                OSPlatform.Windows => ".dll",
                OSPlatform.Linux => ".so",
                OSPlatform.MacOS => ".dylib",
                _ => null
            };

            if (extension == null)
            {
                Log.Warning($"Unable to compute the name of the native loader file. Unsupported platform: " + Environment.OSVersion.Platform);
                return null;
            }

            return NativeLoaderBaseFilename + extension;
        }
    }
}
