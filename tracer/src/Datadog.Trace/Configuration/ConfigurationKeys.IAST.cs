// <copyright file="ConfigurationKeys.IAST.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Configuration
{
    internal partial class ConfigurationKeys
    {
        internal class IAST
        {
            /// <summary>
            /// Configuration key for enabling or disabling the IAST.
            /// Default is value is false (disabled).
            /// </summary>
            public const string Enabled = "DD_IAST_ENABLED";

            /// <summary>
            /// Configuration key for enabling or disabling the Weak hash algorithms detection.
            /// Default is value is true (enabled).
            /// </summary>
            public const string WeakHashAlgorithms = "DD_IAST_WEAK_HASH_ALGORITHMS";
        }
    }
}
