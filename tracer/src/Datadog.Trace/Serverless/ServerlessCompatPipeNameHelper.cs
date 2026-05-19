// <copyright file="ServerlessCompatPipeNameHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Serverless
{
    /// <summary>
    /// Checks whether the Datadog Serverless Compat layer is available and supports named-pipe
    /// transport. Pipe-name generation itself lives in SettingsManager.CreatePipeNames.
    /// </summary>
    internal static class ServerlessCompatPipeNameHelper
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServerlessCompatPipeNameHelper));

        /// <summary>
        /// Checks whether the Datadog Serverless Compat layer is deployed and has a version
        /// that supports named pipe transport. Callers are expected to have already confirmed
        /// the platform supports named pipes (Windows); this method only probes the compat
        /// binary and DLL on disk.
        /// </summary>
        /// <param name="compatPathOverride">An override for the compat binary path, typically read
        /// from <c>DD_SERVERLESS_COMPAT_PATH</c> via the telemetry-enabled configuration readers
        /// in <see cref="Configuration.ExporterSettings.Raw"/>. <c>null</c> means use the default path.</param>
        internal static bool IsCompatLayerAvailableWithPipeSupport(string? compatPathOverride)
            => IsCompatLayerAvailableWithPipeSupport(compatPathOverride, File.Exists, path => AssemblyName.GetAssemblyName(path).Version);

        /// <summary>
        /// Testable overload that accepts I/O dependencies as delegates.
        /// </summary>
        internal static bool IsCompatLayerAvailableWithPipeSupport(
            string? compatPathOverride,
            Func<string, bool> fileExists,
            Func<string, Version?> getAssemblyVersion)
        {
            try
            {
                // Check that the compat binary exists — it's what actually listens on the named pipe.
                // DD_SERVERLESS_COMPAT_PATH overrides the default binary location
                // (matches CompatibilityLayer.cs in datadog-serverless-compat-dotnet).
                const string defaultCompatBinaryPath = @"C:\home\site\wwwroot\datadog\bin\windows-amd64\datadog-serverless-compat.exe";
                var compatBinaryPath = !StringUtil.IsNullOrEmpty(compatPathOverride) ? compatPathOverride : defaultCompatBinaryPath;

                // Check that the compat DLL exists and has a version that supports named pipes.
                // Named pipe support was added in compat version 1.5.0 (dev builds use 0.0.0).
                var compatDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "Datadog.Serverless.Compat.dll");
                if (!fileExists(compatBinaryPath) || !fileExists(compatDllPath))
                {
                    Log.Debug("Did not find Serverless Compatibility Layer or related DLLs.");
                    return false;
                }

                var version = getAssemblyVersion(compatDllPath);

                if (version is null)
                {
                    Log.Debug("Could not read Serverless Compatibility Layer details at {Path}, using fallback agent communication methods. (No Named Pipes)", compatDllPath);
                    return false;
                }

                // Allow 0.0.0 (dev builds) or >= 1.5.0 (first release with pipe support)
                var isDevBuild = version.Major == 0 && version.Minor == 0 && version.Build == 0;
                var isSupported = version.Major > 1 || (version.Major == 1 && version.Minor >= 5);

                if (isDevBuild || isSupported)
                {
                    Log.Debug("Compat layer version {Version} supports named pipes.", version);
                    return true;
                }

                Log.Debug("Compat layer version {Version} does not support named pipes (requires v1.5.0 or greater. Using fallback communication methods.)", version);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to determine Serverless Compatibility layer availability or Named Pipe Support.");
                return false;
            }
        }
    }
}
