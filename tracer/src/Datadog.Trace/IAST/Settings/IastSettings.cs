// <copyright file="IastSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Iast.Settings
{
    internal class IastSettings
    {
        public static readonly string InsecureHashingAlgorithmsDefault = "HMACMD5,MD5,HMACSHA1,SHA1";

        public IastSettings(IConfigurationSource source)
        {
            Enabled = source?.GetBool(ConfigurationKeys.Iast.Enabled) ?? false;
            InsecureHashingAlgorithms = (source?.GetString(ConfigurationKeys.Iast.WeakHashAlgorithms) ?? InsecureHashingAlgorithmsDefault).Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        public bool Enabled { get; set; }

        public string[] InsecureHashingAlgorithms { get; }

        public static IastSettings FromDefaultSources()
        {
            return new IastSettings(GlobalConfigurationSource.Instance);
        }
    }
}
