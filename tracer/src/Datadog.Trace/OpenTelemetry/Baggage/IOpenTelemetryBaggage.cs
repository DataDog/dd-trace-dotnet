// <copyright file="IOpenTelemetryBaggage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.OpenTelemetry.Baggage;

internal interface IOpenTelemetryBaggage
{
    // static methods in OpenTelemetry.Baggage
    IReadOnlyDictionary<string, string>? GetBaggage(IOpenTelemetryBaggage baggage);

    string? GetBaggage(string name, IOpenTelemetryBaggage baggage);

    IOpenTelemetryBaggage SetBaggage(string name, string? value, IOpenTelemetryBaggage baggage);

    IOpenTelemetryBaggage RemoveBaggage(string name, IOpenTelemetryBaggage baggage);

    IOpenTelemetryBaggage ClearBaggage(IOpenTelemetryBaggage baggage);

    // instance methods in OpenTelemetry.Baggage
    IReadOnlyDictionary<string, string>? GetBaggage();

    string? GetBaggage(string name);

    IOpenTelemetryBaggage SetBaggage(string name, string? value);

    IOpenTelemetryBaggage RemoveBaggage(string name);

    IOpenTelemetryBaggage ClearBaggage();
}
