// <copyright file="EmptyDatadogTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

// <copyright file="EmptyDatadogTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace.Asm
{
    public class EmptyDatadogTracer : IDatadogTracer
    {
        public string DefaultServiceName => "My Service Name";

        public TracerSettings Settings => new(new NullConfigurationSource());

        IGitMetadataTagsProvider IDatadogTracer.GitMetadataTagsProvider => new NullGitMetadataProvider();

        PerTraceSettings IDatadogTracer.PerTraceSettings => null;

        void IDatadogTracer.Write(ArraySegment<Span> span)
        {
           
        }
    }
}
