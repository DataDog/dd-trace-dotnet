// <copyright file="CommandTokenizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;

#nullable enable

namespace Datadog.Trace.Iast.SensitiveData;

internal class CommandTokenizer : ITokenizer
{
    private static Regex _pattern = new Regex(@"^(?:\s*(?:sudo|doas|cmd|cmd.exe)\s+)?\b\S+\b(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public List<Range> GetTokens(string value, IntegrationId? integrationId = null)
    {
        var res = new List<Range>(1);
        var match = _pattern.Match(value);
        if (match != null && match.Success)
        {
            var group = match.Groups[1];
            if (group != null && group.Success)
            {
                int start = group.Index;
                int end = group.Index + group.Length;

                res.Add(new Range(start, end - start, null));
            }
        }

        return res;
    }
}
