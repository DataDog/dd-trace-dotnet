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
    private static bool _showTimeoutExceptionError = true;
    private readonly Regex _keysRegex;
    private readonly Regex _valuesRegex;
    private readonly IDatadogLogger? _logger;
    private TimeSpan _timeout;
    private Dictionary<string, ITokenizer> _tokenizers;

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

        var urlTokenizer = new UrlTokenizer(_timeout);
        _tokenizers = new Dictionary<string, ITokenizer>
        {
            { VulnerabilityTypeName.SqlInjection, new SqlInjectionTokenizer(_timeout) },
            { VulnerabilityTypeName.LdapInjection, new LdapTokenizer(_timeout) },
            { VulnerabilityTypeName.CommandInjection, new CommandTokenizer(_timeout) },
            { VulnerabilityTypeName.Ssrf, urlTokenizer },
            { VulnerabilityTypeName.UnvalidatedRedirect, urlTokenizer },
            { VulnerabilityTypeName.HeaderInjection, new HeaderInjectionTokenizer(_timeout) },
            { VulnerabilityTypeName.NoSqlMongoDbInjection, new NoSqlInjectionRedactor() },
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
            LogTimeoutError(err);
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
            LogTimeoutError(err);
            return true;
        }
    }

    private void LogTimeoutError(RegexMatchTimeoutException err)
    {
        if (_showTimeoutExceptionError)
        {
            _logger?.Warning(err, "Regex timed out when trying to match value against pattern {Pattern}.", err.Pattern);
            _showTimeoutExceptionError = false;
        }
        else
        {
            _logger?.Debug(err, "Regex timed out when trying to match value against pattern {Pattern}.", err.Pattern);
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
        if (string.IsNullOrEmpty(evidenceValue))
        {
            return vulnerability;
        }

        List<Range>? sensitive = null;

        if (_tokenizers.TryGetValue(vulnerability.Type, out var tokenizer))
        {
            try
            {
                sensitive = tokenizer.GetTokens(evidenceValue!, vulnerability.GetIntegrationId());
            }
            catch (RegexMatchTimeoutException ex)
            {
                LogTimeoutError(ex);
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
