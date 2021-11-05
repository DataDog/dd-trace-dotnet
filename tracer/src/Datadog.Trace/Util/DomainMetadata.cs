// <copyright file="DomainMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;

namespace Datadog.Trace.Util
{
#if !NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Dedicated helper class for consistently referencing Process and AppDomain information.
    /// </summary>
    internal static class DomainMetadata
    {
        private const string UnknownName = "unknown";
        private static bool _initialized;
        private static string _currentProcessName;
        private static string _currentProcessMachineName;
        private static int _currentProcessId;

        private static bool _processDataUnavailable;
        private static bool _domainDataPoisoned;
        private static bool? _isAppInsightsAppDomain;

        static DomainMetadata()
        {
            TrySetProcess();

            if (_processDataUnavailable)
            {
                _currentProcessName = UnknownName;
                _currentProcessMachineName = UnknownName;
                _currentProcessId = -1;
            }
        }

        public static string ProcessName => _currentProcessName;

        public static string MachineName => _currentProcessMachineName;

        public static int ProcessId => _currentProcessId;

        public static string AppDomainName
        {
            get
            {
                try
                {
                    return !_domainDataPoisoned ? AppDomain.CurrentDomain.FriendlyName : UnknownName;
                }
                catch
                {
                    _domainDataPoisoned = true;
                    return UnknownName;
                }
            }
        }

        public static int AppDomainId
        {
            get
            {
                try
                {
                    return !_domainDataPoisoned ? AppDomain.CurrentDomain.Id : -1;
                }
                catch
                {
                    _domainDataPoisoned = true;
                    return -1;
                }
            }
        }

        public static bool ShouldAvoidAppDomain()
        {
            var isAppInsightsAppDomain = _isAppInsightsAppDomain;

            if (isAppInsightsAppDomain == null)
            {
                isAppInsightsAppDomain = AppDomainName.IndexOf("ApplicationInsights", StringComparison.OrdinalIgnoreCase) >= 0;
                _isAppInsightsAppDomain = isAppInsightsAppDomain;
            }

            return isAppInsightsAppDomain.Value;
        }

        private static void TrySetProcess()
        {
            try
            {
                if (!_processDataUnavailable && !_initialized)
                {
                    _initialized = true;
                    ProcessHelpers.GetCurrentProcessInformation(out _currentProcessName, out _currentProcessMachineName, out _currentProcessId);
                }
            }
            catch
            {
                _processDataUnavailable = true;
            }
        }
    }
#else
    /// <summary>
    /// Dedicated helper class for consistently referencing Process and AppDomain information.
    /// </summary>
    internal static class DomainMetadata
    {
        private const string UnknownName = "unknown";
        private static bool _initialized;
        private static bool _processDataUnavailable;

        private static string _currentProcessName;
        private static string _currentProcessMachineName;
        private static int _currentProcessId;
        private static string _appDomainName;
        private static int _appDomainId;
        private static bool _isAppInsightsAppDomain;

        static DomainMetadata()
        {
            TrySetProcess();

            if (_processDataUnavailable)
            {
                _currentProcessName = UnknownName;
                _currentProcessMachineName = UnknownName;
                _currentProcessId = -1;
            }

            try
            {
                _appDomainName = AppDomain.CurrentDomain.FriendlyName;
            }
            catch
            {
                _appDomainName = UnknownName;
            }

            try
            {
                _appDomainId = AppDomain.CurrentDomain.Id;
            }
            catch
            {
                _appDomainId = -1;
            }

            _isAppInsightsAppDomain = _appDomainName.IndexOf("ApplicationInsights", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string ProcessName => _currentProcessName;

        public static string MachineName => _currentProcessMachineName;

        public static int ProcessId => _currentProcessId;

        public static string AppDomainName => _appDomainName;

        public static int AppDomainId => _appDomainId;

        public static bool ShouldAvoidAppDomain() => _isAppInsightsAppDomain;

        private static void TrySetProcess()
        {
            try
            {
                if (!_processDataUnavailable && !_initialized)
                {
                    _initialized = true;
                    ProcessHelpers.GetCurrentProcessInformation(out _currentProcessName, out _currentProcessMachineName, out _currentProcessId);
                }
            }
            catch
            {
                _processDataUnavailable = true;
            }
        }
    }
#endif
}
