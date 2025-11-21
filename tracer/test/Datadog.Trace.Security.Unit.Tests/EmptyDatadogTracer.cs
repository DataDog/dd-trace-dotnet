// <copyright file="EmptyDatadogTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Sampling;
using Moq;

namespace Datadog.Trace.Security.Unit.Tests
{
    internal class EmptyDatadogTracer : IDatadogTracer
    {
        public EmptyDatadogTracer()
        {
            DefaultServiceName = "My Service Name";
            Settings = new TracerSettings(NullConfigurationSource.Instance);
            var namingSchema = new NamingSchema(SchemaVersion.V0, false, false, DefaultServiceName, null, null);
            PerTraceSettings = new PerTraceSettings(null, null, namingSchema, MutableSettings.CreateWithoutDefaultSources(Settings));
        }

        public string DefaultServiceName { get; }

        public TracerSettings Settings { get; }

        IGitMetadataTagsProvider IDatadogTracer.GitMetadataTagsProvider => new NullGitMetadataProvider();

        public PerTraceSettings PerTraceSettings { get; }

        void IDatadogTracer.Write(in SpanCollection span)
        {
        }
    }
}
