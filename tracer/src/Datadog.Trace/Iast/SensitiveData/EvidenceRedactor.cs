// <copyright file="EvidenceRedactor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;

#nullable enable

namespace Datadog.Trace.Iast.SensitiveData;

internal class EvidenceRedactor
{
    private readonly Regex _keysRegex;
    private readonly Regex _valuesRegex;
    private readonly IDatadogLogger? _logger;
    private TimeSpan _timeout;
    private Dictionary<string, ITokenizer> _tokenizers;

    public EvidenceRedactor(string keysPattern, string valuesPattern, TimeSpan timeout, IDatadogLogger? logger = null)
    {
        _timeout = timeout;
        _logger = logger;

        var options = RegexOptions.IgnoreCase | RegexOptions.Compiled;

        _keysRegex = new(keysPattern, options, _timeout);
        _valuesRegex = new(valuesPattern, options, _timeout);

        var urlTokenizer = new UrlTokenizer(_timeout);
        _tokenizers = new Dictionary<string, ITokenizer>
        {
            { VulnerabilityTypeName.SqlInjection, new SqlInjectionTokenizer(_timeout) },
            { VulnerabilityTypeName.LdapInjection, new LdapTokenizer(_timeout) },
            { VulnerabilityTypeName.CommandInjection, new CommandTokenizer(_timeout) },
            { VulnerabilityTypeName.Ssrf, urlTokenizer },
            { VulnerabilityTypeName.UnvalidatedRedirect, urlTokenizer },
            { VulnerabilityTypeName.HeaderInjection, new HeaderInjectionTokenizer(_timeout) },
            { VulnerabilityTypeName.NoSqlMongoDbInjection, new JsonTokenizer(_timeout) },
            { VulnerabilityTypeName.Xss, new TaintedRangeBasedTokenizer() },
            { VulnerabilityTypeName.EmailHtmlInjection, new TaintedRangeBasedTokenizer() }
        };
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
        catch (RegexMatchTimeoutException err)
        {
            IastModule.LogTimeoutError(err);
            return true;
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
        catch (RegexMatchTimeoutException err)
        {
            IastModule.LogTimeoutError(err);
            return true;
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
        var evidenceValue = vulnerability.Evidence?.Value;
        if (vulnerability.Evidence is null || string.IsNullOrEmpty(vulnerability.Evidence?.Value))
        {
            return vulnerability;
        }

        List<Range>? sensitive = null;

        if (_tokenizers.TryGetValue(vulnerability.Type, out var tokenizer))
        {
            try
            {
                sensitive = tokenizer.GetTokens(vulnerability.Evidence!.Value, vulnerability.GetIntegrationId());
            }
            catch (RegexMatchTimeoutException ex)
            {
                IastModule.LogTimeoutError(ex);
                // We redact the whole vulnerability if the tokenizer times out
                return new Vulnerability(vulnerability.Type, vulnerability.Location, new Evidence(evidenceValue!, vulnerability.Evidence?.Ranges, vulnerability.Evidence?.Ranges), vulnerability.GetIntegrationId());
            }
        }

        // We can skip new vulnerabilities creation for vulnerability types without redaction
        if (sensitive == null)
        {
            return vulnerability;
        }

        return new Vulnerability(vulnerability.Type, vulnerability.Location, new Evidence(evidenceValue!, vulnerability.Evidence?.Ranges, sensitive?.ToArray()), vulnerability.GetIntegrationId());
    }
}
