// <copyright file="FileConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that
    /// retrieves values from environment variables.
    /// </summary>
    internal class FileConfigurationSource : StringConfigurationSource
    {
        // Initial buffer size, large to make sure it's enough. We could check in the
        // native code if the buffer is large enough and re-exec if not, but this will
        // be done later.
        private static readonly int _bufferSize = 100;

        // File path override for testing purposes
        private string? _filePathOverride;

        private ConfigEntry[] _configList;

        public FileConfigurationSource(string? filePathOverride = null)
        {
            _filePathOverride = filePathOverride;
            _configList = LoadConfiguration();
        }

        /// <inheritdoc />
        internal override ConfigurationOrigins Origin => ConfigurationOrigins.EnvVars; // TODO CHANGEME

        private ConfigEntry[] LoadConfiguration()
        {
            int configEntriesCount = 0;
            var configEntries = new NativeConfigEntry[_bufferSize];
            int result = FrameworkDescription.Instance.IsWindows() ?
                                    Windows.LoadConfigurationFromDisk(_filePathOverride ?? string.Empty, _bufferSize, ref configEntriesCount, configEntries) :
                                    NonWindows.LoadConfigurationFromDisk(_filePathOverride ?? string.Empty, _bufferSize, ref configEntriesCount, configEntries);

            if (result != 0 || configEntries == null)
            {
                throw new Exception($"Failed to load configuration data (code {result}).");
            }

            var configList = new ConfigEntry[configEntriesCount];
            for (int i = 0; i < configEntriesCount; i++)
            {
                string key = Marshal.PtrToStringAnsi(configEntries[i].Key) ?? string.Empty;
                string value = Marshal.PtrToStringAnsi(configEntries[i].Value) ?? string.Empty;
                configList[i] = new ConfigEntry
                {
                    Key = key,
                    Value = value,
                };
            }

            return configList;
        }

        /// <inheritdoc />
        protected override string? GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            foreach (var config in _configList)
            {
                if (config.Key == key)
                {
                    return config.Value;
                }
            }

            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeConfigEntry
        {
            public IntPtr Key;
            public IntPtr Value;
        }

        internal struct ConfigEntry
        {
            public string Key;
            public string Value;
        }

        // the "dll" extension is required on .NET Framework
        // and optional on .NET Core
        // These methods are rewritten by the native tracer to use the correct paths
        private static partial class Windows
        {
            [DllImport("Datadog.Tracer.Native.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern int LoadConfigurationFromDisk(string filePathOverride, int bufferSize, ref int configEntriesCount, [In, Out] NativeConfigEntry[] configEntries);
        }

        // assume .NET Core if not running on Windows
        // These methods are rewritten by the native tracer to use the correct paths
        private static partial class NonWindows
        {
            [DllImport("Datadog.Tracer.Native")] // Somehow only the absolute path works..?
            public static extern int LoadConfigurationFromDisk(string filePathOverride, int bufferSize, ref int configEntriesCount, [In, Out] NativeConfigEntry[] configEntries);
        }
    }
}
