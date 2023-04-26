// <copyright file="IastSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Iast.Settings;

internal class IastSettings
{
    public const string WeakCipherAlgorithmsDefault = "DES,TRIPLEDES,RC2";
    public const string WeakHashAlgorithmsDefault = "HMACMD5,MD5,HMACSHA1,SHA1";
    public const int VulnerabilitiesPerRequestDefault = 2;
    public const int MaxConcurrentRequestDefault = 2;
    public const int RequestSamplingDefault = 30;

    public IastSettings(IConfigurationSource source, IConfigurationTelemetry telemetry)
    {
        var config = new ConfigurationBuilder(source, telemetry);
        WeakCipherAlgorithms = config.WithKeys(ConfigurationKeys.Iast.WeakCipherAlgorithms).AsString().Get(WeakCipherAlgorithmsDefault);
        WeakCipherAlgorithmsArray = WeakCipherAlgorithms.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        WeakHashAlgorithms = config.WithKeys(ConfigurationKeys.Iast.WeakHashAlgorithms).AsString().Get(WeakHashAlgorithmsDefault);
        WeakHashAlgorithmsArray = WeakHashAlgorithms.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        Enabled = config.WithKeys(ConfigurationKeys.Iast.Enabled).AsBool().Get(false);
        DeduplicationEnabled = config.WithKeys(ConfigurationKeys.Iast.IsIastDeduplicationEnabled).AsBool().Get(true);
        RequestSampling = config
                         .WithKeys(ConfigurationKeys.Iast.RequestSampling)
                         .AsInt32()
                         .Get(RequestSamplingDefault, x => x is > 0 and <= 100)
                         .Value;
        MaxConcurrentRequests = config
                               .WithKeys(ConfigurationKeys.Iast.MaxConcurrentRequests)
                               .AsInt32()
                               .Get(MaxConcurrentRequestDefault, x => x > 0)
                               .Value;
        VulnerabilitiesPerRequest = config
                                   .WithKeys(ConfigurationKeys.Iast.VulnerabilitiesPerRequest)
                                   .AsInt32()
                                   .Get(VulnerabilitiesPerRequestDefault, x => x > 0)
                                   .Value;
    }

    public bool Enabled { get; set; }

    public string[] WeakHashAlgorithmsArray { get; }

    public bool DeduplicationEnabled { get; }

    public string WeakHashAlgorithms { get; }

    public string[] WeakCipherAlgorithmsArray { get; }

    public string WeakCipherAlgorithms { get; }

    public int RequestSampling { get; }

    public int MaxConcurrentRequests { get; }

    public int VulnerabilitiesPerRequest { get; }

    public static IastSettings FromDefaultSources()
    {
        return new IastSettings(GlobalConfigurationSource.Instance, TelemetryFactoryV2.GetConfigTelemetry());
    }
}
