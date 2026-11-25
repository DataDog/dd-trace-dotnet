// <copyright file="BaggageScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace;

/// <summary>
/// Captures the ambient <see cref="Baggage"/> instance so it can be restored once a unit of work
/// completes. Message consumers and job workers typically reuse a single execution context across
/// sequential units of work, so baggage extracted for one unit would otherwise leak into the next.
/// </summary>
internal sealed class BaggageScope : IDisposable
{
    private readonly Baggage _previous;

    private BaggageScope(Baggage previous)
    {
        _previous = previous;
    }

    /// <summary>
    /// Captures the ambient baggage and makes <paramref name="baggage"/> current in its place, so
    /// the unit of work sees only the baggage it was given.
    /// </summary>
    /// <param name="baggage">The baggage to make current, or <c>null</c> to start from empty.</param>
    /// <returns>A scope that puts the captured baggage back when disposed.</returns>
    public static BaggageScope Capture(Baggage? baggage)
    {
        var previous = Baggage.Current;

        Baggage.Current = baggage ?? new Baggage();
        return new BaggageScope(previous);
    }

    /// <summary>
    /// Puts the captured baggage back in the calling execution context.
    /// </summary>
    public void Dispose()
    {
        Baggage.Current = _previous;
    }
}
