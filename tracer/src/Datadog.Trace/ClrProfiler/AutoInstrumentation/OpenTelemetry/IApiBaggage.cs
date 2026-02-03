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
    /// </summary>
    internal interface IApiBaggage
    {
        IApiBaggage Create(Dictionary<string, string?>? baggageItems);

        IReadOnlyDictionary<string, string?> GetBaggage();

        string? GetBaggage(string name);

        IBaggageHolder EnsureBaggageHolder();
    }

    internal interface IBaggageHolder
    {
        [DuckField(Name = "Baggage")]
        IApiBaggage Baggage { get; set; }
    }
}
