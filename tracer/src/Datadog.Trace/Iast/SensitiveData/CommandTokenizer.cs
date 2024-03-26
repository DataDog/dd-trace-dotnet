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

/// <summary>
/// Tokenizer for COMMAND_INJECTION
/// It locates the parameters issued to a command being passed to the interpreter
/// cmd echo “sensitive value”  -> cmd echo ?
/// </summary>
internal class CommandTokenizer : ITokenizer
{
    private const string _patternCommand = @"^(?:\s*(?:sudo|doas|cmd|cmd.exe)\s+)?\b\S+\b\s+(.*)";
    private Regex _patternRegex;

    public CommandTokenizer(TimeSpan timeout)
    {
        _patternRegex = new(_patternCommand, RegexOptions.Compiled | RegexOptions.IgnoreCase, timeout);
    }

    public List<Range> GetTokens(Evidence evidence, IntegrationId? integrationId = null)
    {
        var value = evidence.Value;
        if (value is null) { return []; }

        var res = new List<Range>(1);
        var match = _patternRegex.Match(value);
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
