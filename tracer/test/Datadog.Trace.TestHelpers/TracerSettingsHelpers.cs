// <copyright file="TracerSettingsHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.LibDatadog;

// ReSharper disable once CheckNamespace
namespace Datadog.Trace.Configuration;

internal static class TracerSettingsHelpers
{
    extension(TracerSettings)
    {
        public static TracerSettings Create(Dictionary<string, object> settings)
            => Create(settings, LibDatadogAvailabilityHelper.IsLibDatadogAvailable);

        public static TracerSettings Create(Dictionary<string, object> settings, LibDatadogAvailableResult isLibDatadogAvailable) =>
            new(
                new DictionaryConfigurationSource(settings.ToDictionary(x => x.Key, x => FormattableString.Invariant($"{x.Value}"))),
                new ConfigurationTelemetry(),
                new OverrideErrorLog(),
                isLibDatadogAvailable);
    }
}
