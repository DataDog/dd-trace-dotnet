// <copyright file="RcmCapabilitiesIndices.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Numerics;

namespace Datadog.Trace.RemoteConfigurationManagement
{
    internal static class RcmCapabilitiesIndices
    {
#pragma warning disable SA1203 // Constants should appear before fields
        public const uint ReservedUInt32 = 0;
        public static readonly BigInteger Reserved = new(ReservedUInt32);

        public const uint AsmActivationUInt32 = 1 << 1;
        public static readonly BigInteger AsmActivation = new(AsmActivationUInt32);

        public const uint AsmIpBlockingUInt32 = 1 << 2;
        public static readonly BigInteger AsmIpBlocking = new(AsmIpBlockingUInt32);

        public const uint AsmDdRulesUInt32 = 1 << 3;
        public static readonly BigInteger AsmDdRules = new(AsmDdRulesUInt32);

        public const uint AsmExclusionsUInt32 = 1 << 4;
        public static readonly BigInteger AsmExclusion = new(AsmExclusionsUInt32);

        public const uint AsmRequestBlockingUInt32 = 1 << 5;
        public static readonly BigInteger AsmRequestBlocking = new(AsmRequestBlockingUInt32);

        public const uint AsmResponseBlockingUInt32 = 1 << 6;
        public static readonly BigInteger AsmResponseBlocking = new(AsmResponseBlockingUInt32);

        public const uint AsmUserBlockingUInt32 = 1 << 7;
        public static readonly BigInteger AsmUserBlocking = new(AsmUserBlockingUInt32);

        public const uint AsmCustomBlockingResponseUInt32 = 1 << 9;
        public static readonly BigInteger AsmCustomBlockingResponse = new(AsmCustomBlockingResponseUInt32);
#pragma warning restore SA1203 // Constants should appear before fields
    }
}
