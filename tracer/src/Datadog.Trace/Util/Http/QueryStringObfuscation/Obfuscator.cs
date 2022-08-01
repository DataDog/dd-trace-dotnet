// <copyright file="Obfuscator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// turns out strict formatting and optional compilation don't like each other
#pragma warning disable SA1001, SA1116, SA1118

using System;
#if !NETCOREAPP3_1_OR_GREATER
using System.Text.RegularExpressions;
#endif
using Datadog.Trace.Logging;
#if NETCOREAPP3_1_OR_GREATER
using Datadog.Trace.Vendors.IndieSystem.Text.RegularExpressions;
#endif

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
#if NETCOREAPP3_1_OR_GREATER
            AppDomain.CurrentDomain.SetData("REGEX_NONBACKTRACKING_MAX_AUTOMATA_SIZE", 2000);
#endif
            _timeout = timeout;
            _logger = logger;

            var options =
                    RegexOptions.IgnoreCase
                    | RegexOptions.Compiled;

#if NETCOREAPP3_1_OR_GREATER
            options |= RegexOptions.NonBacktracking;
#endif

            _regex =
                new(pattern, options, _timeout);
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
