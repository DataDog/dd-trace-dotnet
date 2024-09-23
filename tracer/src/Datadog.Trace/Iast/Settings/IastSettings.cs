// <copyright file="IastSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Iast.Settings;

internal class IastSettings
{
    public const string WeakCipherAlgorithmsDefault = "DES,TRIPLEDES,RC2";
    public const string WeakHashAlgorithmsDefault = "HMACMD5,MD5,HMACSHA1,SHA1";
    public const int VulnerabilitiesPerRequestDefault = 2;
    public const int MaxConcurrentRequestDefault = 2;
    public const int MaxRangeCountDefault = 10;
    public const int RequestSamplingDefault = 30;
    public const int TruncationMaxValueLengthDefault = 250;
    public const int DataBaseRowsToTaintDefault = 1;

    /// <summary>
    /// Default keys readaction regex if none specified via env DD_IAST_REDACTION_KEYS_REGEXP
    /// </summary>
    internal const string DefaultRedactionKeysRegex = @"(?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)?|auth(?:entication|orization)?|(?:sur|last)name|user(?:name)?|address|e?mail)";

    /// <summary>
    /// Default values readaction regex if none specified via env DD_IAST_REDACTION_VALUES_REGEXP
    /// </summary>
    internal const string DefaultRedactionValuesRegex = @"(?i)(?:bearer\s+[a-z0-9\._\-]+|glpat-[\w\-]{20}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L][\w=\-]+\.ey[I-L][\w=\-]+(?:\.[\w.+/=\-]+)?|(?:[\-]{5}BEGIN[a-z\s]+PRIVATE\sKEY[\-]{5}[^\-]+[\-]{5}END[a-z\s]+PRIVATE\sKEY[\-]{5}|ssh-rsa\s*[a-z0-9/\.+]{100,})|[\w\.-]+@[a-zA-Z\d\.-]+\.[a-zA-Z]{2,})";

    /// <summary>
    /// Default values readaction regex if none specified via env DD_IAST_REDACTION_VALUES_REGEXP
    /// </summary>
    internal const string DefaultCookieFilterRegex = @".{32,}";

    public IastSettings(IConfigurationSource source, IConfigurationTelemetry telemetry)
    {
        var config = new ConfigurationBuilder(source, telemetry);
        WeakCipherAlgorithms = config.WithKeys(ConfigurationKeys.Iast.WeakCipherAlgorithms).AsString(WeakCipherAlgorithmsDefault);
        WeakCipherAlgorithmsArray = WeakCipherAlgorithms.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        WeakHashAlgorithms = config.WithKeys(ConfigurationKeys.Iast.WeakHashAlgorithms).AsString(WeakHashAlgorithmsDefault);
        WeakHashAlgorithmsArray = WeakHashAlgorithms.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        Enabled = config.WithKeys(ConfigurationKeys.Iast.Enabled).AsBool(false);
        DeduplicationEnabled = config.WithKeys(ConfigurationKeys.Iast.IsIastDeduplicationEnabled).AsBool(true);
        RequestSampling = config
                         .WithKeys(ConfigurationKeys.Iast.RequestSampling)
                         .AsInt32(RequestSamplingDefault, x => x is > 0 and <= 100)
                         .Value;
        MaxConcurrentRequests = config
                               .WithKeys(ConfigurationKeys.Iast.MaxConcurrentRequests)
                               .AsInt32(MaxConcurrentRequestDefault, x => x > 0)
                               .Value;
        MaxRangeCount = config
                        .WithKeys(ConfigurationKeys.Iast.MaxRangeCount)
                        .AsInt32(MaxRangeCountDefault, x => x > 0)
                        .Value;
        VulnerabilitiesPerRequest = config
                                   .WithKeys(ConfigurationKeys.Iast.VulnerabilitiesPerRequest)
                                   .AsInt32(VulnerabilitiesPerRequestDefault, x => x > 0)
                                   .Value;
        RedactionEnabled = config
                           .WithKeys(ConfigurationKeys.Iast.RedactionEnabled)
                           .AsBool(true);
        RedactionKeysRegex = config
                             .WithKeys(ConfigurationKeys.Iast.RedactionKeysRegex)
                             .AsString(DefaultRedactionKeysRegex);
        RedactionValuesRegex = config
                               .WithKeys(ConfigurationKeys.Iast.RedactionValuesRegex)
                               .AsString(DefaultRedactionValuesRegex);
        RegexTimeout = config
                                .WithKeys(ConfigurationKeys.Iast.RegexTimeout)
                                .AsDouble(200, val1 => val1 is >= 0).Value;

        TelemetryVerbosity = config
            .WithKeys(ConfigurationKeys.Iast.TelemetryVerbosity)
            .GetAs(
                getDefaultValue: () => IastMetricsVerbosityLevel.Information,
                converter: value => value.ToLowerInvariant() switch
                {
                    "off" => IastMetricsVerbosityLevel.Off,
                    "debug" => IastMetricsVerbosityLevel.Debug,
                    "mandatory" => IastMetricsVerbosityLevel.Mandatory,
                    "information" => IastMetricsVerbosityLevel.Information,
                    _ => ParsingResult<IastMetricsVerbosityLevel>.Failure()
                },
                validator: null);

        TruncationMaxValueLength = config
            .WithKeys(ConfigurationKeys.Iast.TruncationMaxValueLength)
            .AsInt32(TruncationMaxValueLengthDefault, x => x > 0)
            .Value;

        DataBaseRowsToTaint = config
            .WithKeys(ConfigurationKeys.Iast.DataBaseRowsToTaint)
            .AsInt32(DataBaseRowsToTaintDefault, x => x >= 0)
            .Value;

        CookieFilterRegex = config
            .WithKeys(ConfigurationKeys.Iast.CookieFilterRegex)
            .AsString(DefaultCookieFilterRegex);
    }

    public bool Enabled { get; set; }

    public string[] WeakHashAlgorithmsArray { get; }

    public bool DeduplicationEnabled { get; }

    public string WeakHashAlgorithms { get; }

    public string[] WeakCipherAlgorithmsArray { get; }

    public string WeakCipherAlgorithms { get; }

    public int RequestSampling { get; }

    public int MaxConcurrentRequests { get; }

    public int MaxRangeCount { get; }

    public int VulnerabilitiesPerRequest { get; }

    public bool RedactionEnabled { get; }

    public string RedactionKeysRegex { get; }

    public string RedactionValuesRegex { get; }

    public double RegexTimeout { get; }

    public IastMetricsVerbosityLevel TelemetryVerbosity { get; }

    public int TruncationMaxValueLength { get; }

    public int DataBaseRowsToTaint { get; }

    public string CookieFilterRegex { get; }

    public static IastSettings FromDefaultSources()
    {
        return new IastSettings(GlobalConfigurationSource.Instance, TelemetryFactory.Config);
    }
}
