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

        // Matches URL-encoded high-byte sequences (%80–%FF) which represent multibyte (non-ASCII)
        // characters such as Korean, Japanese, or Chinese filenames.
        // Credential patterns (tokens, keys, passwords) are always ASCII; these sequences cannot
        // contain sensitive data and are safe to strip before applying the obfuscation regex.
        // Stripping them eliminates the trigger condition for catastrophic backtracking on
        // the default obfuscation pattern. See: https://github.com/DataDog/dd-trace-dotnet/issues/XXXXX
        private static readonly Regex HighByteUrlEncodingRegex = new(
            @"(?:%[89A-Fa-f][0-9A-Fa-f])+",
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(200));

        private readonly Regex _regex;
        private readonly TimeSpan _timeout;
        private readonly IDatadogLogger _logger;

        internal Obfuscator(string pattern, TimeSpan timeout, IDatadogLogger logger)
        {
            _timeout = timeout;
            _logger = logger;

            // NonBacktracking (available in .NET 7+) guarantees O(n) matching time and eliminates
            // catastrophic backtracking on URL-encoded multibyte content (e.g. Korean/CJK filenames).
            // The default obfuscation regex contains alternation patterns such as (?:%2[^2]|%[^2]|[^"%])+
            // and ey[I-L](?:[\w=-]|%3D)+ that backtrack catastrophically when applied to
            // URL-encoded high-byte sequences like %EC%84%A4%EA%B3%84..., causing sustained CPU spikes.
            // See: https://github.com/DataDog/dd-trace-dotnet/issues/XXXXX
#if NET7_0_OR_GREATER
            const RegexOptions options = RegexOptions.Compiled |
                                         RegexOptions.IgnoreCase |
                                         RegexOptions.IgnorePatternWhitespace |
                                         RegexOptions.NonBacktracking;
#else
            const RegexOptions options = RegexOptions.Compiled |
                                         RegexOptions.IgnoreCase |
                                         RegexOptions.IgnorePatternWhitespace;
#endif

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
                // Strip URL-encoded high-byte sequences before applying the obfuscation regex.
                // Multibyte-encoded characters (%80–%FF) cannot contain credential patterns
                // (tokens, keys, passwords are ASCII-only) and their presence triggers catastrophic
                // backtracking in the default obfuscation regex when combined with auth parameters.
                var sanitized = HighByteUrlEncodingRegex.Replace(queryString, string.Empty);
                return _regex.Replace(sanitized, ReplacementString);
            }
            catch (RegexMatchTimeoutException exception)
            {
                _logger.Error(exception, "Query string obfuscation timed out with timeout value of {TotalMilliseconds} ms and regex pattern {Pattern}", _timeout.TotalMilliseconds, _regex.ToString());
            }

            return string.Empty;
        }
    }
}
