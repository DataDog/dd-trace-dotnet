// <copyright file="EvidenceRedactor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
#if !NETCOREAPP3_1_OR_GREATER
using System.Text.RegularExpressions;
#endif
using Datadog.Trace.Logging;
#if NETCOREAPP3_1_OR_GREATER
using Datadog.Trace.Vendors.IndieSystem.Text.RegularExpressions;
#endif

#nullable enable

namespace Datadog.Trace.Iast.SensitiveData;

internal class EvidenceRedactor
{
    private readonly Regex _keysRegex;
    private readonly Regex _valuesRegex;
    private readonly TimeSpan _timeout;
    private readonly IDatadogLogger? _logger;

    private Dictionary<string, ITokenizer> _tokenizers = new Dictionary<string, ITokenizer>
    {
        { VulnerabilityTypeName.SqlInjection, new SqlInjectionTokenizer() },
        { VulnerabilityTypeName.LdapInjection, new LdapTokenizer() },
        { VulnerabilityTypeName.CommandInjection, new CommandTokenizer() },
        { VulnerabilityTypeName.Ssrf, new UrlTokenizer() },
        { VulnerabilityTypeName.UnvalidatedRedirect, new UrlTokenizer() },
    };

    public EvidenceRedactor(string keysPattern, string valuesPattern, TimeSpan timeout, IDatadogLogger? logger = null)
    {
#if NETCOREAPP3_1_OR_GREATER
        AppDomain.CurrentDomain.SetData("REGEX_NONBACKTRACKING_MAX_AUTOMATA_SIZE", 2000);
#endif
        _timeout = timeout;
        _logger = logger;

        var options = RegexOptions.IgnoreCase | RegexOptions.Compiled;

#if NETCOREAPP3_1_OR_GREATER
        options |= RegexOptions.NonBacktracking;
#endif

        _keysRegex = new(keysPattern, options, _timeout);
        _valuesRegex = new(valuesPattern, options, _timeout);
    }

    public bool IsKeySensitive(string? key)
    {
        if (key == null)
        {
            return false;
        }

        try
        {
            return _keysRegex.IsMatch(key);
        }
        catch (Exception err)
        {
            _logger?.Error("Timeout in Evidence Key Redaction Regex. {V}", err.ToString());
            return false;
        }
    }

    public bool IsValueSensitive(string? value)
    {
        if (value == null)
        {
            return false;
        }

        try
        {
            return _valuesRegex.IsMatch(value);
        }
        catch (Exception err)
        {
            _logger?.Error("Timeout in Evidence Values Redaction Regex. {V}", err.ToString());
            return false;
        }
    }

    public void Process(Source source)
    {
        if (IsKeySensitive(source.Name) || IsValueSensitive(source.Value))
        {
            source.MarkAsSensitive();
        }
    }

    internal Vulnerability RedactVulnerability(Vulnerability vulnerability)
    {
        if (vulnerability.Evidence?.Value is null)
        {
            return vulnerability;
        }

        List<Range>? sensitive = null;
        string evidenceValue = vulnerability.Evidence?.Value!;

        if (_tokenizers.TryGetValue(vulnerability.Type, out var tokenizer))
        {
            sensitive = tokenizer.GetTokens(evidenceValue, vulnerability.GetIntegrationId());
        }

        // We can skip new vulnerabilities creeation for vulnerability types without redaction
        if (sensitive == null)
        {
            return vulnerability;
        }

        return new Vulnerability(vulnerability.Type, vulnerability.Location, new Evidence(evidenceValue, vulnerability.Evidence?.Ranges, sensitive?.ToArray()), vulnerability.GetIntegrationId());
    }
}
