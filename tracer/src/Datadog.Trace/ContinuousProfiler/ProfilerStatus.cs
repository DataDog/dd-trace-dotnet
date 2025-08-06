// <copyright file="ProfilerStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ContinuousProfiler
{
    internal class ProfilerStatus : IProfilerStatus
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProfilerStatus));

        private readonly ProfilerState _profilerState;
        private readonly object _lockObj;
        private bool _isInitialized;
        private IntPtr _engineStatusPtr;

        public ProfilerStatus(ProfilerSettings settings)
        {
            _profilerState = settings.ProfilerState;
            var state = _profilerState switch
            {
                ProfilerState.Enabled => "enabled",
                ProfilerState.Auto => "auto",
                _ => "disabled"
            };

            Log.Information("Continuous Profiler mode = {ProfilerState}", state);
            _lockObj = new();
            _isInitialized = false;
        }

        public bool IsProfilerReady
        {
            get
            {
                if (_profilerState == ProfilerState.Disabled)
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
