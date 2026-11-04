// <copyright file="ExposureCommitMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags;

internal sealed class ExposureCommitMetadata(IDictionary<string, string>? metadata, Action exposureCommit)
    : Dictionary<string, string>(metadata ?? new Dictionary<string, string>())
{
    public Action ExposureCommit { get; } = exposureCommit;
}
