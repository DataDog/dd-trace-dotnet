// <copyright file="Obfuscator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util.Http.QueryStringObfuscation
{
    internal sealed class Obfuscator : ObfuscatorBase
    {
        private const string ReplacementString = "<redacted>";
        private readonly Regex _regex;
        private readonly TimeSpan _timeout;
        private readonly IDatadogLogger _logger;

        internal Obfuscator(string pattern, TimeSpan timeout, IDatadogLogger logger)
        {
            _timeout = timeout;
            _logger = logger;

            // CultureInvariant is required so that case-insensitive matching does not depend on the
            // ambient culture. Without it, non-ASCII input forces per-character culture-aware case
            // folding (a measurable CPU spike on .NET Framework), and Turkic cultures mis-case the
            // dotted/dotless 'I' so that keywords containing 'i' (api, public, signature, ...) are
            // never redacted.
            const RegexOptions options = RegexOptions.Compiled |
                                         RegexOptions.IgnoreCase |
                                         RegexOptions.IgnorePatternWhitespace |
                                         RegexOptions.CultureInvariant;

            _regex = new Regex(pattern, options, _timeout);

            try
            {
                // Warmup the regex
                // Can't use empty string, space, or dot, as they are optimized and don't actually trigger the compilation
                _ = _regex.Match("o");
            }
            catch
            {
                // Nothing to log here
            }
        }

        /// <summary>
        /// WARNING: This regex cause crashes under netcoreapp2.1 / linux / arm64, dont use on manual instrumentation in this environment
        /// </summary>
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
                _logger.Error(exception, "Query string obfuscation timed out with timeout value of {TotalMilliseconds} ms and regex pattern {Pattern}", _timeout.TotalMilliseconds, _regex.ToString());
            }

            return string.Empty;
        }
    }
}
