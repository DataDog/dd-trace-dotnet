// <copyright file="ExposureEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Datadog.Trace.FeatureFlags.Exposure;

internal class ExposureEvent(long timeStamp, Allocation allocation, Flag flag, Variant variant, Subject subject)
{
    public long TimeStamp { get; } = timeStamp;

    public Allocation Allocation { get; } = allocation;

    public Flag Flag { get; } = flag;

    public Variant Variant { get; } = variant;

    public Subject Subject { get; } = subject;
}
