// <copyright file="QueryStringObfuscator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text.RegularExpressions;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util.Http
{
    internal class QueryStringObfuscator
    {
        /// <summary>
        /// Default obfuscation query string regex if none specified via env DD_OBFUSCATION_QUERY_STRING_REGEXP
        /// </summary>
        public const string DefaultObfuscationQueryStringRegex = @"((?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)?|auth(?:entication|orization)?)(?:(?:\s|%20)*(?:=|%3D)[^&]+|(?:""|%22)(?:\s|%20)*(?::|%3A)(?:\s|%20)*(?:""|%22)(?:%2[^2]|%[^2]|[^""%])+(?:""|%22))|bearer(?:\s|%20)+[a-z0-9\._\-]|token(?::|%3A)[a-z0-9]{13}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L](?:[\w=-]|%3D)+\.ey[I-L](?:[\w=-]|%3D)+(?:\.(?:[\w.+\/=-]|%3D|%2F|%2B)+)?|[\-]{5}BEGIN(?:[a-z\s]|%20)+PRIVATE(?:\s|%20)KEY[\-]{5}[^\-]+[\-]{5}END(?:[a-z\s]|%20)+PRIVATE(?:\s|%20)KEY|ssh-rsa(?:\s|%20)*(?:[a-z0-9\/\.+]|%2F|%5C|%2B){100,})";

        private static QueryStringObfuscator _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();
        private readonly Obfuscator _obfuscator;

        private QueryStringObfuscator(string pattern = null)
        {
            pattern ??= Tracer.Instance.Settings.ObfuscationQueryStringRegex;
            _obfuscator = new(pattern);
        }

        internal string Obfuscate(string queryString) => _obfuscator.Obfuscate(queryString);

        /// <summary>
        /// Gets or sets the global <see cref="QueryStringObfuscator"/> instance.
        /// </summary>
        public static QueryStringObfuscator Instance(string pattern = null) => LazyInitializer.EnsureInitialized(ref _instance, ref _globalInstanceInitialized, ref _globalInstanceLock, () => new(pattern));

        internal class Obfuscator
        {
            private const string ReplacementString = "<redacted>";
            private readonly Regex _regex;
            private readonly IDatadogLogger _log = DatadogLogging.GetLoggerFor(typeof(Obfuscator));
            private readonly bool _disabled;

            internal Obfuscator(string pattern = null)
            {
                if (string.IsNullOrEmpty(pattern))
                {
                    _disabled = true;
                }
                else
                {
                    _regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
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
                    return _regex.Replace(queryString, ReplacementString);
                }
                catch (RegexMatchTimeoutException)
                {
                    Log();
                }

                return string.Empty;
            }

            internal virtual void Log()
            {
                _log.Error("Query string could not be redacted using regex {pattern}", _regex.ToString());
            }
        }
    }
}
