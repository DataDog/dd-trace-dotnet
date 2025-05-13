// <copyright file="TaintedRangeBasedTokenizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;

#nullable enable

namespace Datadog.Trace.Iast.SensitiveData;

/// <summary>
/// Tokenizer based on evidence tainted ranges
/// </summary>
internal class TaintedRangeBasedTokenizer : ITokenizer
{
    public TaintedRangeBasedTokenizer()
    {
    }

    public List<Range> GetTokens(Evidence evidence, IntegrationId? integrationId = null)
    {
        var value = evidence.Value;
        var ranges = evidence.Ranges;
        if (value is null || ranges is null) { return []; }

        var res = new List<Range>(ranges.Length);
        int pos = 0;
        foreach (var range in ranges)
        {
            if (range.Start <= pos)
            {
                pos = range.Start + range.Length;
            }
            else
            {
                var next = new Range(pos, range.Start - pos);
                pos = range.Start + range.Length;
                res.Add(next);
            }
        }

        if (pos < value.Length)
        {
            var next = new Range(pos, value.Length - pos);
            res.Add(next);
        }

        return res;
    }
}
