// <copyright file="ExposureRecordResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags.Exposure;

// NotRecorded is not "new": nothing reached the exposure cache, for example after exposure reporting
// stopped at shutdown, so there is no verdict to give the customer hook.
internal enum ExposureRecordResult
{
    NotRecorded,
    New,
    Repeat,
}
