// <copyright file="SchemaTrackingCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;

namespace Datadog.Trace.DataStreamsMonitoring;

internal class SchemaTrackingCommon
{
    internal interface IDescriptorWrapper
    {
        string? Name { get; }

        Schema? GetSchema();
    }

    /// <summary>
    /// Get the current span, add some tags about the schema,
    /// and once in a while, tag it with the json of the whole schema used
    /// </summary>
    internal static void EnrichActiveSpanWith(IDescriptorWrapper descriptor, string operationName, string schematype)
    {
        var tracer = Tracer.Instance;

        var settings = tracer.Settings;
        if (!settings.IsDataStreamsMonitoringEnabled)
        {
            return;
        }

        var activeSpan = (tracer.ActiveScope as Scope)?.Span;
        if (activeSpan == null)
        {
            return;
        }

        if (activeSpan.GetTag(Tags.SchemaType) == schematype)
        {
            // we already instrumented this, we are most likely in a recursive call due to nested schemas.
            return;
        }

        activeSpan.SetTag(Tags.SchemaType, schematype);
        activeSpan.SetTag(Tags.SchemaName, descriptor.Name);
        activeSpan.SetTag(Tags.SchemaOperation, operationName);

        // check rate limit
        var dsm = tracer.TracerManager.DataStreamsManager;
        if (!dsm.ShouldExtractSchema(activeSpan, operationName, out var weight))
        {
            return;
        }

        var schema = descriptor.GetSchema();
        // note: if schema is null, we still "consumed" the sampling. If this happens too often,
        // we might get a sampling ratio a lot lower than expected.
        // It is the responsibility of the implementation of GetSchema to log if necessary.
        if (schema != null)
        {
            activeSpan.SetTag(Tags.SchemaDefinition, schema.Definition);
            activeSpan.SetTag(Tags.SchemaId, schema.Id);
            activeSpan.SetTag(Tags.SchemaWeight, weight.ToString(CultureInfo.InvariantCulture));
        }
    }
}
