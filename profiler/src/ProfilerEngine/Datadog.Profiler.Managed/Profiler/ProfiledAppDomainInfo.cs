// <copyright file="ProfiledAppDomainInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Profiler
{
    internal class ProfiledAppDomainInfo : ProfiledEntityInfoBase
    {
        private readonly ulong _profilerAppDomainId;

        public ProfiledAppDomainInfo(ulong profilerAppDomainId, int providerSessionId)
            : base(providerSessionId)
        {
            _profilerAppDomainId = profilerAppDomainId;

            this.AppDomainProcessId = 0;
            this.AppDomainName = string.Empty;
        }

        public ulong ProfilerAppDomainId
        {
            get { return _profilerAppDomainId; }
        }

        public ulong AppDomainProcessId { get; private set; }
        public string AppDomainProcessIdString { get; private set; }
        public string AppDomainName { get; private set; }

        public static string FormatProcessIdForUnknownAppDomain(ulong profilerAppDomainId)
        {
            return "<0x" + profilerAppDomainId.ToString("X") + ">";
        }

        public static string FormatNameForUnknownAppDomain(ulong profilerAppDomainId)
        {
            return "<Unknown> [#0x" + profilerAppDomainId.ToString("X") + "]";
        }

        internal void UpdateProperties(int providerSessionId, ulong appDomainProcessId, string appDomainName)
        {
            ProviderSessionId = providerSessionId;

            AppDomainProcessId = appDomainProcessId;
            AppDomainProcessIdString = FormatAppDomainProcessId(appDomainProcessId);
            AppDomainName = FormatName(appDomainName);
        }

        private static string FormatAppDomainProcessId(ulong appDomainProcessId)
        {
            return appDomainProcessId.ToString("D");
        }

        private static string FormatName(string rawAppDomainName)
        {
            return (rawAppDomainName == null) ? string.Empty : rawAppDomainName.Trim();
        }
    }
}