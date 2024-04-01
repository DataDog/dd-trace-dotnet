// <copyright file="UrlTokenizer.cs" company="Datadog">
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
/// Tokenizer for SSRF vulnerability
/// It locates in a Url the authority part (user:pwd) and the values of the query parameters
/// https://user:password@datadoghq.com:443/api/v1/test/123/?param1=pone -> https://?@datadoghq.com:443/api/v1/test/123/?param1=?
/// </summary>
internal class UrlTokenizer : ITokenizer
{
    private const string AuthorityRegex = "^(?:[^:]+:)?//(?<AUTHORITY>[^@]+)@";
    private const string QueryFragmentGroup = "[?#&$]([^=&;]+)=(?<QUERY>[^?#&]+)";
    private Regex _patternUrl;

    public UrlTokenizer(TimeSpan timeout)
    {
        _patternUrl = new Regex(string.Join("|", AuthorityRegex, QueryFragmentGroup), RegexOptions.Compiled | RegexOptions.IgnoreCase, timeout);
    }

    public List<Range> GetTokens(Evidence evidence, IntegrationId? integrationId = null)
    {
        var value = evidence.Value;
        if (value is null) { return []; }

        var res = new List<Range>(1);
        foreach (Match? match in _patternUrl.Matches(value))
        {
            if (match != null && match.Success)
            {
                var group = match.Groups["AUTHORITY"];
                if (group == null || !group.Success)
                {
                    group = match.Groups["QUERY"];
                }

                if (group != null && group.Success)
                {
                    int start = group.Index;
                    int end = group.Index + group.Length;

                    res.Add(new Range(start, end - start, null));
                }
            }
        }

        return res;
    }
}
