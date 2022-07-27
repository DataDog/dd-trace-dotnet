// <copyright file="Obfuscator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.IndieSystem.Text.RegularExpressions;

namespace Datadog.Trace.Util.Http.QueryStringObfuscation
{
    internal class Obfuscator : ObfuscatorBase
    {
        private const string ReplacementString = "<redacted>";
        private readonly Regex _regex;
        private readonly TimeSpan _timeout;
        private readonly IDatadogLogger _logger;

        internal Obfuscator(string pattern, TimeSpan timeout, IDatadogLogger logger)
        {
            AppDomain.CurrentDomain.SetData("REGEX_NONBACKTRACKING_MAX_AUTOMATA_SIZE", 2000);
            _timeout = timeout;
            _logger = logger;
            _regex = new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.NonBacktracking, _timeout);
        }

        internal override string Obfuscate(string queryString)
        {
            if (string.IsNullOrEmpty(queryString))
            {
                return queryString;
            }

            try
            {
                return _regex.Replace(queryString, ReplacementString);
            }
            catch (RegexMatchTimeoutException exception)
            {
                _logger.Error(exception, "Query string obfuscation timed out with timeout value of {TotalMilliseconds} ms and regex pattern {pattern}", _timeout.TotalMilliseconds, _regex.ToString());
            }

            return string.Empty;
        }
    }
}
