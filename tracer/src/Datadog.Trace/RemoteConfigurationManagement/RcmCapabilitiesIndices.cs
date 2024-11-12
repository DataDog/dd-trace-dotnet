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

        public static readonly BigInteger AsmExclusionData = Create(18);

        public static readonly BigInteger ApmTracingTracingEnabled = Create(19);

        public static readonly BigInteger ApmTracingDataStreamsEnabled = Create(20);

        public static readonly BigInteger AsmRaspSqli = Create(21);

        public static readonly BigInteger AsmRaspLfi = Create(22);

        public static readonly BigInteger AsmRaspSsrf = Create(23);

        public static readonly BigInteger AsmRaspShi = Create(24);

        public static readonly BigInteger AsmRaspXxe = Create(25);

        public static readonly BigInteger AsmRaspRce = Create(26);

        public static readonly BigInteger AsmRaspNosqli = Create(27);

        public static readonly BigInteger AsmRaspXss = Create(28);

        public static readonly BigInteger ApmTracingSampleRules = Create(29);

        public static readonly BigInteger CsmActivation = Create(30);

        public static readonly BigInteger AsmAutoUserInstrumentationMode = Create(31);

        public static readonly BigInteger AsmEnpointFingerprint = Create(32);

        public static readonly BigInteger AsmSessionFingerprint = Create(33);

        public static readonly BigInteger AsmNetworkFingerprint = Create(34);

        public static readonly BigInteger AsmHeaderFingerprint = Create(35);

        private static BigInteger Create(int index) => new(1UL << index);
    }
}
