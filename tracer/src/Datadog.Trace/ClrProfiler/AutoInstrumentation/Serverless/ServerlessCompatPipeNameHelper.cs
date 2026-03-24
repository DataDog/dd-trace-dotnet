// <copyright file="ServerlessCompatPipeNameHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Serverless
{
    /// <summary>
    /// Helper class for generating unique pipe names for serverless compat layer coordination.
    /// Shared logic for both trace and metrics pipe name generation.
    /// </summary>
    internal static class ServerlessCompatPipeNameHelper
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServerlessCompatPipeNameHelper));

        /// <summary>
        /// Generates a unique pipe name by appending a GUID to the base name.
        /// Validates and truncates the base name if necessary to ensure the full pipe path stays within Windows limits.
        /// </summary>
        /// <param name="baseName">The base name for the pipe</param>
        /// <param name="pipeType">The type of pipe for logging (e.g., "trace" or "DogStatsD")</param>
        /// <returns>A unique pipe name in the format {base}_{guid}</returns>
        internal static string GenerateUniquePipeName(string baseName, string pipeType)
        {
            // Validate base pipe name length before appending GUID
            // Windows pipe path format: \\.\pipe\{base}_{guid}
            // Max total: 256 - 9 (\\.\pipe\) - 1 (underscore) - 32 (GUID) = 214
            const int maxBaseLength = 214;

            if (baseName.Length > maxBaseLength)
            {
                Log.Warning<string, int, int>("{PipeType} pipe base name exceeds {MaxLength} characters ({ActualLength}). Truncating to allow for GUID suffix.", pipeType, maxBaseLength, baseName.Length);
                baseName = baseName.Substring(0, maxBaseLength);
            }

            var guid = Guid.NewGuid().ToString("N"); // "N" format removes hyphens (32 chars)
            var uniqueName = $"{baseName}_{guid}";

            return uniqueName;
        }

        /// <summary>
        /// Checks whether the Datadog Serverless Compat layer is deployed and has a version
        /// that supports named pipe transport. This is called during ExporterSettings construction
        /// (before the compat assembly is loaded) so it checks files on disk rather than
        /// loaded assemblies.
        /// </summary>
        internal static bool IsCompatLayerAvailableWithPipeSupport()
            => IsCompatLayerAvailableWithPipeSupport(File.Exists, path => AssemblyName.GetAssemblyName(path).Version);

        /// <summary>
        /// Testable overload that accepts I/O dependencies as delegates.
        /// </summary>
        internal static bool IsCompatLayerAvailableWithPipeSupport(
            Func<string, bool> fileExists,
            Func<string, Version?> getAssemblyVersion)
        {
            try
            {
                // Named pipes are Windows-only
#if !NETFRAMEWORK
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return false;
                }
#endif

                // Check that the compat binary exists — it's what actually listens on the named pipe.
                // DD_SERVERLESS_COMPAT_PATH overrides the default binary location
                // (matches CompatibilityLayer.cs in datadog-serverless-compat-dotnet).
                const string defaultCompatBinaryPath = @"C:\home\site\wwwroot\datadog\bin\windows-amd64\datadog-serverless-compat.exe";
                var compatBinaryPath = Util.EnvironmentHelpers.GetEnvironmentVariable(Configuration.ConfigurationKeys.ServerlessCompatPath)
                    ?? defaultCompatBinaryPath;

                // Check that the compat DLL exists and has a version that supports named pipes.
                // Named pipe support was added in compat version 1.4.0 (dev builds use 0.0.0).
                var compatDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "Datadog.Serverless.Compat.dll");
                if (!fileExists(compatBinaryPath) || !fileExists(compatDllPath))
                {
                    Log.Debug("Did not find Serverless Compatibility Layer or related DLLs.");
                    return false;
                }

                var version = getAssemblyVersion(compatDllPath);

                if (version is null)
                {
                    Log.Warning("Could not read Serverless Compatibility Layer details at {Path}, using fallback agent communication methods. (No Named Pipes)", compatDllPath);
                    return false;
                }

                // Allow 0.0.0 (dev builds) or >= 1.4.0 (first release with pipe support)
                var isDevBuild = version.Major == 0 && version.Minor == 0 && version.Build == 0;
                var isSupported = version.Major > 1 || (version.Major == 1 && version.Minor >= 4);

                if (isDevBuild || isSupported)
                {
                    Log.Debug("Compat layer version {Version} supports named pipes.", version);
                    return true;
                }

                Log.Debug("Compat layer version {Version} does not support named pipes (requires v1.4.0 or greater. Using fallback communication methods.)", version);
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to determine Serverless Compatibility layer availability or Named Pipe Support.");
                return false;
            }
        }
    }
}
