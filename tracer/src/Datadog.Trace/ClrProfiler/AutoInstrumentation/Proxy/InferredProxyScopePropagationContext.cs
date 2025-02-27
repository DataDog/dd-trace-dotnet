// <copyright file="InferredProxyScopePropagationContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

internal readonly struct InferredProxyScopePropagationContext(Scope scope, PropagationContext context)
{
    public readonly PropagationContext Context = context;

    public readonly Scope Scope = scope;
}
