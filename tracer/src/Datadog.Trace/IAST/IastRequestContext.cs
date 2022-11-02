// <copyright file="IastRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Iast;

internal class IastRequestContext
{
    private VulnerabilityBatch? _vulnerabilityBatch;
    private object _vulnerabilityLock = new();

    internal void AddIastTagsToSpan(Span span)
    {
        if (Iast.Instance.Settings.Enabled)
        {
            AddIastInfoToRootSpan(span);
        }
    }

    private void AddIastInfoToRootSpan(Span span)
    {
        // Right now, we always set the IastEnabled tag to "1", but in the future, it will not be added if iast is enabled but the request is not analyzed.
        span.Tags.SetTag(Tags.IastEnabled, "1");

        if (_vulnerabilityBatch != null)
        {
            span.Tags.SetTag(Tags.IastJson, _vulnerabilityBatch.ToString());
        }
    }

    internal void AddVulnerability(Vulnerability vulnerability)
    {
        lock (_vulnerabilityLock)
        {
            if (_vulnerabilityBatch == null)
            {
                _vulnerabilityBatch = new();
            }

            _vulnerabilityBatch.Add(vulnerability);
        }
    }
}
