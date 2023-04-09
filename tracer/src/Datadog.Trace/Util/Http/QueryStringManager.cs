// <copyright file="QueryStringManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Http.QueryStringObfuscation;

namespace Datadog.Trace.Util.Http
{
    internal class QueryStringManager
    {
        private readonly bool _reportQueryString;
        private readonly int _maxSizeBeforeObfuscation;
        private readonly Lazy<ObfuscatorBase> _obfuscatorLazy;

        internal QueryStringManager(bool reportQueryString, double timeout, int maxSizeBeforeObfuscation, string pattern, IDatadogLogger logger = null)
        {
            _reportQueryString = reportQueryString;
            _maxSizeBeforeObfuscation = maxSizeBeforeObfuscation;
            _obfuscatorLazy = new(() => ObfuscatorFactory.GetObfuscator(timeout, pattern, logger, _reportQueryString));
        }

        internal string TruncateAndObfuscate(string queryString)
        {
            if (!_reportQueryString || string.IsNullOrEmpty(queryString))
            {
                return string.Empty;
            }

            if (_maxSizeBeforeObfuscation > 0)
            {
                queryString = queryString.Substring(0, Math.Min(queryString.Length, _maxSizeBeforeObfuscation));
            }

            return _obfuscatorLazy.Value.Obfuscate(queryString);
        }
    }
}
