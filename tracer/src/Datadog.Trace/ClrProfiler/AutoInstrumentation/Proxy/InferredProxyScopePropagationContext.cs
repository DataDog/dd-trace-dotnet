// <copyright file="InferredProxyScopePropagationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

internal class InferredProxyScopePropagationContext
{
    public InferredProxyScopePropagationContext(Scope scope, PropagationContext context)
    {
        Scope = scope;
        Context = context;
    }

    public PropagationContext Context { get; }

    public Scope Scope { get; }
}
