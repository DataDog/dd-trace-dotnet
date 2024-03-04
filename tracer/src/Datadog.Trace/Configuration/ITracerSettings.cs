// <copyright file="ITracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

/// <summary>
/// This interface is used to access automatic settings from the manual instrumentation without allocating
/// a dictionary or boxing the values. Changing this interface will break the manual instrumentation.
/// </summary>
internal interface ITracerSettings
{
    public bool TryGetObject(string key, out object? value);

    public bool TryGetInt(string key, out int value);

    public bool TryGetDouble(string key, out double value);

    public bool TryGetBool(string key, out bool value);

    public bool TryGetNullableInt(string key, out int? value);

    public bool TryGetNullableDouble(string key, out double? value);

    public bool TryGetNullableBool(string key, out bool? value);
}
