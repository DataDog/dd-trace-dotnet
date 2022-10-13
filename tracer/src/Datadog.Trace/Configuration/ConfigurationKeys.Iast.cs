// <copyright file="ConfigurationKeys.Iast.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration
{
    internal partial class ConfigurationKeys
    {
        internal class Iast
        {
            /// <summary>
            /// Configuration key for enabling or disabling the IAST.
            /// Default is value is false (disabled).
            /// </summary>
            public const string Enabled = "DD_IAST_ENABLED";

            /// <summary>
            /// Configuration key for establishing the weak hashing algorithms.
            /// </summary>
            public const string WeakHashAlgorithms = "DD_IAST_WEAK_HASH_ALGORITHMS";

            /// <summary>
            /// Configuration key for establishing the weak cipher algorithms.
            /// </summary>
            public const string WeakCipherAlgorithms = "DD_IAST_WEAK_CIPHER_ALGORITHMS";
        }
    }
}
