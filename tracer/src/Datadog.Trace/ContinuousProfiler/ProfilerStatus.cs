// <copyright file="ProfilerStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ContinuousProfiler
{
    internal class ProfilerStatus
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProfilerStatus));

        private readonly bool _isProfilingEnabled;
        private readonly object _lockObj;
        private bool _isInitialized;
        private IntPtr _engineStatusPtr;

        public ProfilerStatus()
        {
            _isProfilingEnabled = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.ProfilingEnabled)?.ToBoolean() ?? false;
            Log.Information("Continuous Profiler is {IsEnabled}.", _isProfilingEnabled ? "enabled" : "disabled");
            _lockObj = new();
            _isInitialized = false;
        }

        public bool IsProfilerReady
        {
            get
            {
                if (!_isProfilingEnabled)
                {
                    return false;
                }

                EnsureNativeIsIntialized();
                return _engineStatusPtr != IntPtr.Zero && Marshal.ReadByte(_engineStatusPtr) != 0;
            }
        }

        private void EnsureNativeIsIntialized()
        {
            if (_isInitialized)
            {
                return;
            }

            lock (_lockObj)
            {
                if (_isInitialized)
                {
                    return;
                }

                _isInitialized = true;

                try
                {
                    _engineStatusPtr = NativeInterop.GetProfilerStatusPointer();
                }
                catch (Exception e)
                {
                    Log.Warning(e, "No profiler related feature(s) will be enabled. Failed to retrieve profiler status native pointer.");
                    _engineStatusPtr = IntPtr.Zero;
                }
            }
        }
    }
}
