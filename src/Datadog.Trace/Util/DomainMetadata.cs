using System;
using System.Diagnostics;

namespace Datadog.Trace.Util
{
    internal static class DomainMetadata
    {
        private static Process _currentProcess = null;
        private static bool _poisoned = false;

        static DomainMetadata()
        {
            TrySetProcess();
        }

        public static string ProcessName
        {
            get
            {
                return !_poisoned ? _currentProcess.ProcessName : "unknown";
            }
        }

        public static string MachineName
        {
            get
            {
                return !_poisoned ? _currentProcess.MachineName : "unknown";
            }
        }

        public static int ProcessId
        {
            get
            {
                return _poisoned ? _currentProcess.Id : -1;
            }
        }

        public static string AppDomainName
        {
            get
            {
                try
                {
                    return AppDomain.CurrentDomain.FriendlyName;
                }
                catch
                {
                    return "unknown";
                }
            }
        }

        public static int AppDomainId
        {
            get
            {
                try
                {
                    return AppDomain.CurrentDomain.Id;
                }
                catch
                {
                    return -1;
                }
            }
        }

        public static bool ShouldAvoidAppDomain()
        {
            if (AppDomainName.ToLowerInvariant().Contains("applicationinsights"))
            {
                // unsafe context to operate in
                return true;
            }

            return false;
        }

        private static void TrySetProcess()
        {
            try
            {
                if (!_poisoned && _currentProcess == null)
                {
                    _currentProcess = Process.GetCurrentProcess();
                }
            }
            catch
            {
                _poisoned = true;
            }
        }
    }
}
