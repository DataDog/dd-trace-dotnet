// <copyright file="RcmCapabilitiesIndices.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Numerics;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal static class RcmCapabilitiesIndices
    {
        public static readonly BigInteger Reserved = Create(0);

        public static readonly BigInteger AsmActivation = Create(1);

        public static readonly BigInteger AsmIpBlocking = Create(2);

        public static readonly BigInteger AsmDdRules = Create(3);

        public static readonly BigInteger AsmExclusion = Create(4);

        public static readonly BigInteger AsmRequestBlocking = Create(5);

        public static readonly BigInteger AsmResponseBlocking = Create(6);

        public static readonly BigInteger AsmUserBlocking = Create(7);

        public static readonly BigInteger AsmCustomRules = Create(8);

        public static readonly BigInteger AsmCustomBlockingResponse = Create(9);

        public static readonly BigInteger AsmTrustedIps = Create(10);

        public static readonly BigInteger AsmApiSecuritySampleRate = Create(11);

        public static readonly BigInteger ApmTracingSampleRate = Create(12);

        public static readonly BigInteger ApmTracingLogsInjection = Create(13);

        public static readonly BigInteger ApmTracingHttpHeaderTags = Create(14);

        public static readonly BigInteger ApmTracingCustomTags = Create(15);

        public static readonly BigInteger AsmProcessorOverrides = Create(16);

        public static readonly BigInteger AsmCustomDataScanners = Create(17);

        public static readonly BigInteger ApmTracingEnabled = Create(19);

        private static BigInteger Create(int index) => new(1UL << index);
    }
}
