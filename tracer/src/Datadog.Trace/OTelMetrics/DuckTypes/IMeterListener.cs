// <copyright file="IMeterListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.OTelMetrics.DuckTypes
{
    // Using interface instead of [DuckCopy] struct as we need to set values too
    internal interface IMeterListener : IDuckType
    {
        object InstrumentPublished { get; set; }

        object? DisableMeasurementEvents(object instrument);

        void EnableMeasurementEvents(object instrument, object? state);

        void RecordObservableInstruments();

        void SetMeasurementEventCallback<T>(object? callback)
            where T : struct;

        void Start();
    }
}
#endif
