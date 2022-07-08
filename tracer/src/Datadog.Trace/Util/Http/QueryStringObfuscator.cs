// <copyright file="QueryStringObfuscator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util.Http
{
    internal class QueryStringObfuscator
    {
        private static readonly IDatadogLogger _log = DatadogLogging.GetLoggerFor(typeof(QueryStringObfuscator));
        private static QueryStringObfuscator _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();
        private readonly Obfuscator _obfuscator;

        private QueryStringObfuscator(double timeout, string pattern = null)
        {
            pattern ??= Tracer.Instance.Settings.ObfuscationQueryStringRegex;
            _obfuscator = new(TimeSpan.FromMilliseconds(timeout), pattern);
        }

        internal string Obfuscate(string queryString) => _obfuscator.Obfuscate(queryString);

        /// <summary>
        /// Gets or sets the global <see cref="QueryStringObfuscator"/> instance.
        /// </summary>
        public static QueryStringObfuscator Instance(double timeout, string pattern = null) => LazyInitializer.EnsureInitialized(ref _instance, ref _globalInstanceInitialized, ref _globalInstanceLock, () => new(timeout, pattern));

        internal class Obfuscator
        {
            private const string ReplacementString = "<redacted>";
            private readonly Regex _regex;
            private readonly bool _disabled;
            private readonly TimeSpan _timeout;

            internal Obfuscator(TimeSpan timeout, string pattern = null)
            {
                _timeout = timeout;
                if (string.IsNullOrEmpty(pattern))
                {
                    _disabled = true;
                }
                else
                {
                    _regex = new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, _timeout);
                }
            }

            internal string Obfuscate(string queryString)
            {
                if (_disabled || string.IsNullOrEmpty(queryString))
                {
                    return queryString;
                }

                try
                {
                    queryString = queryString.Substring(0, Math.Min(queryString.Length, 200));
                    return _regex.Replace(queryString, ReplacementString);
                }
                catch (RegexMatchTimeoutException e)
                {
                    Log($"The regex task timed out before {_timeout.TotalMilliseconds} ms and is canceled", e);
                }

                return string.Empty;
            }

            internal virtual void Log(string message = null, Exception exception = null)
            {
                var messageTemplate = string.Concat("Query string could not be redacted using regex {pattern}", message);
                if (exception != null)
                {
                    _log.Error(exception, messageTemplate, _regex.ToString());
                }
                else
                {
                    _log.Error(messageTemplate, _regex.ToString());
                }
            }
        }
    }
}
