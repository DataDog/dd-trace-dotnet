// <copyright file="IASTSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.IAST.Settings
{
    internal class IASTSettings
    {
        public static readonly string InsecureHashingAlgorithmsDefault = "System.Security.Cryptography.HMACMD5,System.Security.Cryptography.MD5,System.Security.Cryptography.HMACSHA1,System.Security.Cryptography.SHA1,System.Security.Cryptography.MD5+Implementation,System.Security.Cryptography.MD5CryptoServiceProvider,System.Security.Cryptography.SHA1+Implementation,System.Security.Cryptography.SHA1CryptoServiceProvider";

        public IASTSettings(IConfigurationSource source)
        {
            Enabled = source?.GetBool(ConfigurationKeys.IAST.Enabled) ?? false;
            InsecureHashingAlgorithms = (source?.GetString(ConfigurationKeys.IAST.WeakHashAlgorithms) ?? InsecureHashingAlgorithmsDefault).Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        public bool Enabled { get; set; }

        public string[] InsecureHashingAlgorithms { get; }

        public static IASTSettings FromDefaultSources()
        {
            return new IASTSettings(GlobalConfigurationSource.Instance);
        }
    }
}
