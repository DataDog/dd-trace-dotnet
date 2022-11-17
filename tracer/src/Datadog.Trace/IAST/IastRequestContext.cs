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

    public void AddIastVulnerabilitiesToSpan(Span span)
    {
        span.Tags.SetTag(Tags.IastEnabled, "1");

        if (_vulnerabilityBatch != null)
        {
            span.Tags.SetTag(Tags.IastJson, _vulnerabilityBatch.ToString());
        }
    }

    internal bool AddVulnerabilitiesAllowed()
    {
        return ((_vulnerabilityBatch?.Vulnerabilities.Count ?? 0) < Iast.Instance.Settings.VulnerabilitiesPerRequest);
    }

    internal void AddVulnerability(Vulnerability vulnerability)
    {
        lock (_vulnerabilityLock)
        {
            _vulnerabilityBatch ??= new();
            _vulnerabilityBatch.Add(vulnerability);
        }
    }
}
