// <copyright file="DomainMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Dedicated helper class for consistently referencing Process and AppDomain information.
    /// </summary>
    internal class DomainMetadata
    {
        private static readonly Lazy<DomainMetadata> _instance = new(isThreadSafe: true);

        private string _currentProcessName;
        private string _currentProcessMachineName;
        private int _currentProcessId;
        private string _appDomainName;
        private int _appDomainId;
        private bool _isAppInsightsAppDomain;

        private DomainMetadata()
        {
            const string UnknownName = "unknown";

            try
            {
                // Get Process data
                ProcessHelpers.GetCurrentProcessInformation(out _currentProcessName, out _currentProcessMachineName, out _currentProcessId);

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
            catch
            {
                _currentProcessName = UnknownName;
                _currentProcessMachineName = UnknownName;
                _currentProcessId = -1;
                _appDomainName = UnknownName;
                _appDomainId = -1;
                _isAppInsightsAppDomain = false;
            }
        }

        public static DomainMetadata Instance => _instance.Value;

        public string ProcessName => _currentProcessName;

        public string MachineName => _currentProcessMachineName;

        public int ProcessId => _currentProcessId;

        public string AppDomainName => _appDomainName;

        public int AppDomainId => _appDomainId;

        public bool ShouldAvoidAppDomain() => _isAppInsightsAppDomain;
    }
}
