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

        public const uint AsmActivationUInt32 = 1;
        public static readonly BigInteger AsmActivation = new(AsmActivationUInt32);

        public const uint AsmIpBlockingUInt32 = 1 << 2;
        public static readonly BigInteger AsmIpBlocking = new(AsmIpBlockingUInt32);

        public const uint AsmDdRulesUInt32 = 1 << 3;
        public static readonly BigInteger AsmDdRules = new(AsmDdRulesUInt32);
#pragma warning restore SA1203 // Constants should appear before fields
    }
}
