﻿// <copyright file="IConfigurationOverrideHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

internal interface IConfigurationOverrideHandler
{
    bool TryHandleOverrides<T>(
        string datadogKey,
        ConfigurationResult<T> datadogConfigResult,
        string otelKey,
        ConfigurationResult<T> otelConfigResult,
        [NotNullWhen(true)] out T? value);
}
