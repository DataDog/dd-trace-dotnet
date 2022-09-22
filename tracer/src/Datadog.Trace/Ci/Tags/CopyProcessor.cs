// <copyright file="CopyProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Tagging;

namespace Datadog.Trace.Ci.Tags;

internal readonly struct CopyProcessor : IItemProcessor<string>, IItemProcessor<double>
{
    private readonly Span _span;

    public CopyProcessor(Span span)
    {
        _span = span;
    }

    public void Process(TagItem<string> item)
    {
        _span.Tags.SetTag(item.Key, item.Value);
    }

    public void Process(TagItem<double> item)
    {
        _span.Tags.SetMetric(item.Key, item.Value);
    }
}
