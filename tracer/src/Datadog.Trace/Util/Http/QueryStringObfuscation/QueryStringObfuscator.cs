// <copyright file="QueryStringObfuscator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util.Http.QueryStringObfuscation
{
    internal class QueryStringObfuscator
    {
        private readonly ObfuscatorBase _obfuscator;

        internal QueryStringObfuscator(double timeout, string pattern = null, IDatadogLogger logger = null)
        {
            pattern ??= Tracer.Instance.Settings.ObfuscationQueryStringRegex;
            _obfuscator = ObfuscatorFactory.GetObfuscator(timeout, pattern, logger);
        }

        internal string Obfuscate(string queryString) => _obfuscator.Obfuscate(queryString);
    }
}
