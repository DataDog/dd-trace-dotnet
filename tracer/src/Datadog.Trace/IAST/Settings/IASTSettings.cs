// <copyright file="IASTSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Http;

namespace Datadog.Trace.IAST.Settings
{
    internal class IASTSettings
    {
        public static readonly string[] InsecureHashingAlgorithms = { "HMACMD5", "MD5", "HMACSHA1", "SHA1" };

        public IASTSettings(IConfigurationSource source)
        {
            Enabled = source?.GetBool(ConfigurationKeys.IAST.Enabled) ?? false;
            InsecureHashingAlgorithmEnabled = source?.GetBool(ConfigurationKeys.IAST.WeakHashAlgorithmsEnabled) ?? true;
        }

        public bool Enabled { get; set; }

        public bool InsecureHashingAlgorithmEnabled { get; set; }

        public static IASTSettings FromDefaultSources()
        {
            return new IASTSettings(GlobalConfigurationSource.Instance);
        }
    }
}
