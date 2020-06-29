using System;
using System.Diagnostics;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Dedicated helper class for consistently referencing Process and AppDomain information.
    /// </summary>
    internal static class DomainMetadata
    {
        private const string IsAppInsightKey = "DD_IsAppInsight";
        private const string UnknownName = "unknown";
        private static Process _currentProcess;
        private static bool _processDataPoisoned;
        private static bool _domainDataPoisoned;

        static DomainMetadata()
        {
            TrySetProcess();
        }

        public static string ProcessName
        {
            get
            {
                try
                {
                    return !_processDataPoisoned ? _currentProcess.ProcessName : UnknownName;
                }
                catch
                {
                    _processDataPoisoned = true;
                    return UnknownName;
                }
            }
        }

        public static string MachineName
        {
            get
            {
                try
                {
                    return !_processDataPoisoned ? _currentProcess.MachineName : UnknownName;
                }
                catch
                {
                    _processDataPoisoned = true;
                    return UnknownName;
                }
            }
        }

        public static int ProcessId
        {
            get
            {
                try
                {
                    return !_processDataPoisoned ? _currentProcess.Id : -1;
                }
                catch
                {
                    _processDataPoisoned = true;
                    return -1;
                }
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
            var appDomain = AppDomain.CurrentDomain;

            if (!(appDomain.GetData(IsAppInsightKey) is bool isAppInsight))
            {
                isAppInsight = AppDomainName.IndexOf("ApplicationInsights", StringComparison.OrdinalIgnoreCase) >= 0;
                appDomain.SetData(IsAppInsightKey, isAppInsight);
            }

            return isAppInsight;
        }

        private static void TrySetProcess()
        {
            try
            {
                if (!_processDataPoisoned && _currentProcess == null)
                {
                    _currentProcess = Process.GetCurrentProcess();
                }
            }
            catch
            {
                _processDataPoisoned = true;
            }
        }
    }
}
