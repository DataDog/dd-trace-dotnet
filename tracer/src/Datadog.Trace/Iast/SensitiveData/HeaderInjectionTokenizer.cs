// <copyright file="HeaderInjectionTokenizer.cs" company="Datadog">
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
/// We are already redacting sensitive values in the evidence (header values),
/// but if the key of the returned header matches a regex, we should also redact the evidence value after ":"
/// </summary>
internal class HeaderInjectionTokenizer : ITokenizer
{
    internal const string KeysRegex = @"(?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)?|auth(?:entication|orization)?)";
    private static Regex _keyPattern = new Regex(KeysRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    internal const string ValuesRegex = @"(?i)(?:bearer\s+[a-z0-9\._\-]+|glpat-[\w\-]{20}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L][\w=\-]+\.ey[I-L][\w=\-]+(?:\.[\w.+/=\-]+)?|(?:[\-]{5}BEGIN[a-z\s]+PRIVATE\sKEY[\-]{5}[^\-]+[\-]{5}END[a-z\s]+PRIVATE\sKEY[\-]{5}|ssh-rsa\s*[a-z0-9/\.+]{100,}))";
    private static Regex _valuePattern = new Regex(KeysRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public HeaderInjectionTokenizer()
    {
    }

    public List<Range> GetTokens(string evidence, IntegrationId? integrationId = null)
    {
        var result = new List<Range>();
        var separatorStart = evidence.IndexOf(IastModule.HeaderInjectionEvidenceSeparator);

        if (separatorStart > 0)
        {
            var separatorEnd = separatorStart + IastModule.HeaderInjectionEvidenceSeparator.Length;
            var valuePart = evidence.Substring(separatorEnd);

            if (_keyPattern.IsMatch(evidence.Substring(0, separatorStart)) ||
                _valuePattern.IsMatch(valuePart))
            {
                result.Add(new Range(separatorEnd, valuePart.Length));
            }
        }

        return result;
    }
}
