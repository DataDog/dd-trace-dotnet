// <copyright file="BaggageExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.OpenTelemetry.Baggage;

namespace Datadog.Trace.ExtensionMethods;

internal static class BaggageExtensions
{
    public static void AddOpenTelemetryBaggage(this Baggage baggage)
    {
        var otelBaggageItems = OpenTelemetryBaggage.GetBaggageItems();

        if (otelBaggageItems != null)
        {
            baggage.AddOrReplace(otelBaggageItems);
        }
    }
}
