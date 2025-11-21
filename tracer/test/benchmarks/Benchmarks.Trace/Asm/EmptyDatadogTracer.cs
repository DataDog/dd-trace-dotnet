// <copyright file="EmptyDatadogTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace.Asm
{
    public class EmptyDatadogTracer : IDatadogTracer
    {
        public static readonly EmptyDatadogTracer Instance = new();

        private readonly IGitMetadataTagsProvider _gitMetadata;
        private readonly PerTraceSettings _perTraceSettings;

        public EmptyDatadogTracer()
        {
            Settings = new(new NullConfigurationSource());
            _gitMetadata = new NullGitMetadataProvider();
            _perTraceSettings = new PerTraceSettings(null, null, null, Settings.InitialMutableSettings);
        }

        public string DefaultServiceName => "My Service Name";

        public TracerSettings Settings { get; }

        IGitMetadataTagsProvider IDatadogTracer.GitMetadataTagsProvider => _gitMetadata;

        PerTraceSettings IDatadogTracer.PerTraceSettings => _perTraceSettings;

        void IDatadogTracer.Write(in SpanCollection span)
        {
           
        }
    }
}
