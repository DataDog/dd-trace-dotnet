// <copyright file="EnvironmentVariablesProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.CiEnvironment;

internal readonly struct EnvironmentVariablesProvider : IValueProvider
{
    public string? GetValue(string key, string? defaultValue = null) => EnvironmentHelpers.GetEnvironmentVariable(key, defaultValue);

    public IDictionary GetValues() => EnvironmentHelpers.GetEnvironmentVariables();
}
