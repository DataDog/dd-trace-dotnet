// <copyright file="StubDatadogTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Tests.Util;

internal class StubDatadogTracer : IDatadogTracer
{
    public StubDatadogTracer()
    : this(new TracerSettings(NullConfigurationSource.Instance))
    {
    }

    public StubDatadogTracer(TracerSettings settings)
    {
        DefaultServiceName = "stub-service";
        Settings = settings;
        var namingSchema = new NamingSchema(SchemaVersion.V0, false, false, DefaultServiceName, null, null);
        PerTraceSettings = new PerTraceSettings(null, null, namingSchema, MutableSettings.CreateWithoutDefaultSources(Settings, new ConfigurationTelemetry()));
    }

    public string DefaultServiceName { get; }

    public TracerSettings Settings { get; }

    public List<SpanCollection> WrittenChunks { get; } = new();

    IGitMetadataTagsProvider IDatadogTracer.GitMetadataTagsProvider => new NullGitMetadataProvider();

    public PerTraceSettings PerTraceSettings { get; }

    void IDatadogTracer.Write(in SpanCollection span)
    {
        WrittenChunks.Add(span);
    }
}
