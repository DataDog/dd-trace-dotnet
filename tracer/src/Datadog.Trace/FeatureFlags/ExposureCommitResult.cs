// <copyright file="ExposureCommitResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags
{
    // Return values of FeatureFlagsSdk.CommitExposure, shared by the tracer and the provider package.
    internal static class ExposureCommitResult
    {
        internal const int NothingPending = 0;
        internal const int New = 1;
        internal const int Repeat = 2;
    }
}
