// <copyright file="IApiBaggage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry.Baggage interface for duck-typing
    /// https://github.com/open-telemetry/opentelemetry-dotnet/blob/db429bf642c1a2c2f71b49f88d63e0a661018298/src/OpenTelemetry.Api/Baggage.cs#L16
    /// </summary>
    internal interface IApiBaggage
    {
        [DuckField(Name = "baggage")]
        Dictionary<string, string> Baggage { get; }

        IApiBaggage Create(Dictionary<string, string?>? baggageItems);

        IReadOnlyDictionary<string, string?> GetBaggage();

        string? GetBaggage(string name);

        IBaggageHolder EnsureBaggageHolder();
    }

    /// <summary>
    /// Baggage holder interface for duck-typing
    /// https://github.com/open-telemetry/opentelemetry-dotnet/blob/db429bf642c1a2c2f71b49f88d63e0a661018298/src/OpenTelemetry.Api/Baggage.cs#L371
    /// </summary>
    internal interface IBaggageHolder
    {
        [DuckField(Name = "Baggage")]
        IApiBaggage Baggage { get; set; }
    }
}
