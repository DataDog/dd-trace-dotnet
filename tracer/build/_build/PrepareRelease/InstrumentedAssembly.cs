// <copyright file="InstrumentedAssembly.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace PrepareRelease
{
    public record InstrumentedAssembly
    {
        public string IntegrationName { get; init; }

        public string TargetAssembly { get; init; }

        public ushort TargetMinimumMajor { get; init; }

        public ushort TargetMinimumMinor { get; init; }

        public ushort TargetMinimumPatch { get; init; }

        public ushort TargetMaximumMajor { get; init; }

        public ushort TargetMaximumMinor { get; init; }

        public ushort TargetMaximumPatch { get; init; }
    }
}
