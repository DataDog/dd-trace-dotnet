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
    private const string _keyPattern = @"(?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)?|auth(?:entication|orization)?)";
    private const string _valuePattern = @"(?i)(?:bearer\s+[a-z0-9\._\-]+|glpat-[\w\-]{20}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L][\w=\-]+\.ey[I-L][\w=\-]+(?:\.[\w.+/=\-]+)?|(?:[\-]{5}BEGIN[a-z\s]+PRIVATE\sKEY[\-]{5}[^\-]+[\-]{5}END[a-z\s]+PRIVATE\sKEY[\-]{5}|ssh-rsa\s*[a-z0-9/\.+]{100,}))";
    private Regex _keyRegex;
    private Regex _valueRegex;

    public HeaderInjectionTokenizer(TimeSpan timeout)
    {
        _keyRegex = new Regex(_keyPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, timeout);
        _valueRegex = new Regex(_valuePattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, timeout);
    }

    public List<Range> GetTokens(Evidence evidence, IntegrationId? integrationId = null)
    {
        var value = evidence.Value;
        if (value is null) { return []; }

        var separatorStart = value.IndexOf(IastModule.HeaderInjectionEvidenceSeparator);

        if (separatorStart > 0)
        {
            var separatorEnd = separatorStart + IastModule.HeaderInjectionEvidenceSeparator.Length;

            // If the key patterns applies to the key or the value pattern applies to the value,
            // we should redact the value

            if (_keyRegex.IsMatch(value.Substring(0, separatorStart)) ||
                _valueRegex.IsMatch(value, separatorEnd))
            {
                return [new Range(separatorEnd, value.Length - separatorEnd)];
            }
        }

        return [];
    }
}
