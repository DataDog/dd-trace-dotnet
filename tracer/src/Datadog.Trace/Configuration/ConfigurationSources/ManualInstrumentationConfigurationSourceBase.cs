// <copyright file="ManualInstrumentationConfigurationSourceBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration.ConfigurationSources;

/// <summary>
/// Wraps the settings passed in from the manual instrumentation API in a configuration source, to make it easier to integrate.
/// </summary>
internal abstract class ManualInstrumentationConfigurationSourceBase : DictionaryObjectConfigurationSource
{
    protected ManualInstrumentationConfigurationSourceBase(IReadOnlyDictionary<string, object> dictionary)
        : base(dictionary, ConfigurationOrigins.Code)
    {
    }
}
