using System;
using System.Diagnostics;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Dedicated helper class for consistently referencing Process and AppDomain information.
    /// </summary>
    internal static class DomainMetadata
    {
        private const string UnknownName = "unknown";
        private static Process _currentProcess;
        private static string _currentProcessName;
        private static string _currentProcessMachineName;
        private static int _currentProcessId;
        private static bool _processDataPoisoned;
        private static bool _domainDataPoisoned;
        private static bool? _isAppInsightsAppDomain;

        static DomainMetadata()
        {
            TrySetProcess();
        }

        public static string ProcessName
        {
            get
            {
                return !_processDataPoisoned ? _currentProcessName : UnknownName;
            }
        }

        public static string MachineName
        {
            get
            {
                return !_processDataPoisoned ? _currentProcessMachineName : UnknownName;
            }
        }

        public static int ProcessId
        {
            get
            {
                return !_processDataPoisoned ? _currentProcessId : -1;
            }
        }

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
            if (_isAppInsightsAppDomain == null)
            {
                _isAppInsightsAppDomain = AppDomainName.IndexOf("ApplicationInsights", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return _isAppInsightsAppDomain.Value;
        }

        private static void TrySetProcess()
        {
            try
            {
                if (!_processDataPoisoned && _currentProcess == null)
                {
                    _currentProcess = ProcessHelpers.GetCurrentProcess();
                    _currentProcessName = ProcessHelpers.GetCurrentProcessName();
                    _currentProcessMachineName = ProcessHelpers.GetCurrentProcessMachineName();
                    _currentProcessId = ProcessHelpers.GetCurrentProcessId();
                }
            }
            catch
            {
                _processDataPoisoned = true;
            }
        }
    }
}
