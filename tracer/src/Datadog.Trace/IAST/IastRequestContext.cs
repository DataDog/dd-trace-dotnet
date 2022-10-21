// <copyright file="IastRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Iast;

internal class IastRequestContext
{
    private VulnerabilityBatch vulnerabilityBatch = new();

    internal void AddIastTagsIfNeeded(Span span)
    {
        if (span.IsRootSpan && span.Type == SpanTypes.Web && Iast.Instance.Settings.Enabled)
        {
            AddIastInfoToRootSpan(span);
        }
    }

    private void AddIastInfoToRootSpan(Span span)
    {
        // Right now, we always set the IastEnabled tag to "1", but in the future, it might be zero to indicate that a request has not been analyzed
        span.SetTag(Tags.IastEnabled, "1");

        if (vulnerabilityBatch.HasVulnerabilities())
        {
            span.SetTag(Tags.IastJson, vulnerabilityBatch.ToString());
        }
    }

    internal void AddVulnerability(Vulnerability vulnerability)
    {
        vulnerabilityBatch.Add(vulnerability);
    }
}
