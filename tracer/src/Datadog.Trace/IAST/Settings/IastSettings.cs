// <copyright file="IastSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration;

namespace Datadog.Trace.Iast.Settings;

internal class IastSettings
{
    public static readonly string WeakCipherAlgorithmsDefault = "DES,TRIPLEDES,RC2";
    public static readonly string WeakHashAlgorithmsDefault = "HMACMD5,MD5,HMACSHA1,SHA1";
    public static readonly int VulnerabilitiesPerRequestDefault = 2;
    public static readonly int MaxConcurrentRequestDefault = 2;
    public static readonly int RequestSamplingDefault = 30;

    public IastSettings(IConfigurationSource source)
    {
        WeakCipherAlgorithms = source.GetString(ConfigurationKeys.Iast.WeakCipherAlgorithms) ?? WeakCipherAlgorithmsDefault;
        WeakCipherAlgorithmsArray = WeakCipherAlgorithms.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        WeakHashAlgorithms = source.GetString(ConfigurationKeys.Iast.WeakHashAlgorithms) ?? WeakHashAlgorithmsDefault;
        WeakHashAlgorithmsArray = WeakHashAlgorithms.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        Enabled = (source.GetBool(ConfigurationKeys.Iast.Enabled) ?? false);
        DeduplicationEnabled = source.GetBool(ConfigurationKeys.Iast.IsIastDeduplicationEnabled) ?? true;
        RequestSampling = source.GetInt32(ConfigurationKeys.Iast.RequestSampling) is { } requestSampling and > 0 and <= 100
            ? requestSampling : RequestSamplingDefault;
        MaxConcurrentRequests = source.GetInt32(ConfigurationKeys.Iast.MaxConcurrentRequests) is { } concurrentRequests and > 0
            ? concurrentRequests : MaxConcurrentRequestDefault;
        VulnerabilitiesPerRequest = source.GetInt32(ConfigurationKeys.Iast.VulnerabilitiesPerRequest) is { } vulnerabilities and > 0
            ? vulnerabilities : VulnerabilitiesPerRequestDefault;
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
        return new IastSettings(GlobalConfigurationSource.Instance);
    }
}
