// <copyright file="OptionalField.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal readonly struct OptionalField<T>(T? value)
    where T : class
{
    public bool IsPresent { get; } = true;

    public T? Value { get; } = value;
}
