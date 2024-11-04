// <copyright file="PropagationContextExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Propagators;

internal static class PropagationContextExtensions
{
    public static PropagationContext MergeBaggageInto(this PropagationContext context, Baggage destination)
    {
        context.Baggage?.MergeInto(destination);
        return context;
    }
}
