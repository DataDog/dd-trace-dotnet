// <copyright file="ObfuscatorFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util.Http.QueryStringObfuscation
{
    internal class ObfuscatorFactory
    {
        internal static ObfuscatorBase GetObfuscator(double timeoutInMs, string pattern, IDatadogLogger logger)
            => string.IsNullOrEmpty(pattern)
                   ? new NullObfuscator()
                   : new Obfuscator(pattern, TimeSpan.FromMilliseconds(timeoutInMs), logger ?? DatadogLogging.GetLoggerFor(typeof(QueryStringObfuscator)));
    }
}
