// <copyright file="DuckTypeAotCompatibilityStatuses.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    internal static class DuckTypeAotCompatibilityStatuses
    {
        internal const string Compatible = "compatible";
        internal const string PendingProxyEmission = "pending_proxy_emission";
        internal const string UnsupportedProxyKind = "unsupported_proxy_kind";
        internal const string MissingProxyType = "missing_proxy_type";
        internal const string MissingTargetType = "missing_target_type";
        internal const string MissingTargetMethod = "missing_target_method";
        internal const string NonPublicTargetMethod = "non_public_target_method";
        internal const string IncompatibleMethodSignature = "incompatible_method_signature";
        internal const string UnsupportedProxyConstructor = "unsupported_proxy_constructor";
        internal const string UnsupportedClosedGenericMapping = "unsupported_closed_generic_mapping";
    }
}
