// <copyright file="QueryStringManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Logging;
using Datadog.Trace.Util.Http.QueryStringObfuscation;

namespace Datadog.Trace.Util.Http
{
    internal class QueryStringManager
    {
        private readonly bool _reportQueryString;
        private readonly ObfuscatorBase _obfuscator;

        internal QueryStringManager(bool reportQueryString, double timeout, string pattern = null, IDatadogLogger logger = null)
        {
            _reportQueryString = reportQueryString;
            pattern ??= Tracer.Instance.Settings.ObfuscationQueryStringRegex;
            _obfuscator = ObfuscatorFactory.GetObfuscator(timeout, pattern, logger, reportQueryString);
        }

        internal string Obfuscate(string queryString) => !_reportQueryString ? string.Empty : _obfuscator.Obfuscate(queryString);
    }
}
