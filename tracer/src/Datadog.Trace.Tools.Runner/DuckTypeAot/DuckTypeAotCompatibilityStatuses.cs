// <copyright file="DuckTypeAotCompatibilityStatuses.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Provides helper operations for duck type aot compatibility statuses.
    /// </summary>
    internal static class DuckTypeAotCompatibilityStatuses
    {
        /// <summary>
        /// Defines the compatible constant.
        /// </summary>
        internal const string Compatible = "compatible";

        /// <summary>
        /// Defines the pending proxy emission constant.
        /// </summary>
        internal const string PendingProxyEmission = "pending_proxy_emission";

        /// <summary>
        /// Defines the unsupported proxy kind constant.
        /// </summary>
        internal const string UnsupportedProxyKind = "unsupported_proxy_kind";

        /// <summary>
        /// Defines the missing proxy type constant.
        /// </summary>
        internal const string MissingProxyType = "missing_proxy_type";

        /// <summary>
        /// Defines the missing target type constant.
        /// </summary>
        internal const string MissingTargetType = "missing_target_type";

        /// <summary>
        /// Defines the missing target method constant.
        /// </summary>
        internal const string MissingTargetMethod = "missing_target_method";

        /// <summary>
        /// Defines the non public target method constant.
        /// </summary>
        internal const string NonPublicTargetMethod = "non_public_target_method";

        /// <summary>
        /// Defines the incompatible method signature constant.
        /// </summary>
        internal const string IncompatibleMethodSignature = "incompatible_method_signature";

        /// <summary>
        /// Defines the unsupported proxy constructor constant.
        /// </summary>
        internal const string UnsupportedProxyConstructor = "unsupported_proxy_constructor";

        /// <summary>
        /// Defines the unsupported closed generic mapping constant.
        /// </summary>
        /// <remarks>This field participates in shared runtime state and must remain thread-safe.</remarks>
        internal const string UnsupportedClosedGenericMapping = "unsupported_closed_generic_mapping";

        /// <summary>
        /// Defines the parity expectation mismatch constant.
        /// </summary>
        internal const string ParityExpectationMismatch = "parity_expectation_mismatch";
    }
}
