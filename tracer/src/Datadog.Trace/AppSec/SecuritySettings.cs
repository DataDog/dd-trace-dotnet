// <copyright file="SecuritySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.AppSec
{
    internal class SecuritySettings
    {
        public SecuritySettings(IConfigurationSource source)
        {
            // both should default to false
            Enabled = source?.GetBool(ConfigurationKeys.AppSecEnabled) ?? false;
            BlockingEnabled = source?.GetBool(ConfigurationKeys.AppSecBlockingEnabled) ?? false;
        }

        public bool Enabled { get; set; }

        public bool BlockingEnabled { get; }

        public static SecuritySettings FromDefaultSources()
        {
            var source = GlobalSettings.CreateDefaultConfigurationSource();
            return new SecuritySettings(source);
        }
    }
}
