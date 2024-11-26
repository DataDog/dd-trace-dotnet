// <copyright file="OpenTelemetryBaggageHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.OpenTelemetry.Baggage;

internal static class OpenTelemetryBaggageHelper
{
    private static readonly Type? OpenTelemetryBaggageType = Type.GetType("OpenTelemetry.Baggage, OpenTelemetry.Api", throwOnError: false);

    internal static IReadOnlyDictionary<string, string>? GetBaggageItems()
    {
        if (OpenTelemetryBaggageType == null)
        {
            return null;
        }

        var result = DuckType.GetOrCreateProxyType(typeof(IOpenTelemetryBaggage), OpenTelemetryBaggageType);

        if (!result.Success)
        {
            return null;
        }

        // Pass in null, as there's no "instance" to duck type here, to create an instance of our proxy
        var proxy = result.CreateInstance<IOpenTelemetryBaggage>(null);

        // invoke methods on the proxy
        return proxy.GetBaggage();
    }
}
