// <copyright file="NoSqlInjectionRedactor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;

#nullable enable

namespace Datadog.Trace.Iast.SensitiveData;

internal class NoSqlInjectionRedactor : ITokenizer
{
    private static readonly Regex SourceValueRegex = new Regex(@"(?i)bearer\s+[a-z0-9\._\-]+|token:[a-z0-9]{13}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L][\w=-]+\.ey[I-L][\w=-]+(\.[\w.+\/=-]+)?|[\-]{5}BEGIN[a-z\s]+PRIVATE\sKEY[\-]{5}[^\-]+[\-]{5}END[a-z\s]+PRIVATE\sKEY|ssh-rsa\s*[a-z0-9\/\.+]{100,}", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex SourceNameRegex = new Regex("(?i)^.*(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)?|auth(?:entication|orization)?)");

    private static void AnalyzeMatches(MatchCollection matches, ICollection<Range> list)
    {
        foreach (Match? match in matches)
        {
            if (match is not { Success: true }) { continue; }
            var start = match.Index;
            var end = match.Index + match.Length;
            list.Add(new Range(start, end));
        }
    }

    public List<Range> GetTokens(string value, IntegrationId? integrationId = null)
    {
        var res = new List<Range>();

        AnalyzeMatches(SourceValueRegex.Matches(value), res);
        AnalyzeMatches(SourceNameRegex.Matches(value), res);

        return res;
    }
}
