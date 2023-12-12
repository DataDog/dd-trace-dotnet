// <copyright file="DebugLogScrubber.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

#if NETCOREAPP3_1_OR_GREATER
using Regex = Datadog.Trace.Vendors.IndieSystem.Text.RegularExpressions.Regex;
using RegexOptions = Datadog.Trace.Vendors.IndieSystem.Text.RegularExpressions.RegexOptions;
#else
using Regex = System.Text.RegularExpressions.Regex;
using RegexOptions = System.Text.RegularExpressions.RegexOptions;
#endif

namespace Datadog.Trace.Logging.TracerFlare;

internal class DebugLogScrubber
{
    private const int RegexTimeoutSeconds = 5; // These should be quick
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebugLogScrubber));
    internal static readonly DebugLogScrubber Instance = new();

    private static readonly Lazy<ReplacerConfig[]> Replacers = new(() => CreateReplacers());

    public static string ScrubString(string? source)
    {
        if (source is null)
        {
            return string.Empty;
        }

        // check if we have one of the hints
        try
        {
            var data = source;
            foreach (var replacerConfig in Replacers.Value)
            {
                if (replacerConfig.Hints is { Length: > 0 } hints)
                {
                    var containsHint = false;
                    foreach (var hint in hints)
                    {
#if NETCOREAPP
                        if (data.Contains(hint, StringComparison.Ordinal))
#else
                    if (data.Contains(hint))
#endif
                        {
                            containsHint = true;
                            break;
                        }
                    }

                    if (!containsHint)
                    {
                        // This regex isn't relevant, ignore
                        continue;
                    }
                }

                // run the regex;
                data = replacerConfig.Regex.Replace(data, replacerConfig.Replacement);
            }

            return data;
        }
        catch (TimeoutException)
        {
            // timeout, play it safe and scrub the whole string
            Log.Warning("Timeout executing debug log scrubber");
            return string.Empty;
        }
        catch (Exception ex)
        {
            // uh oh, play it safe and scrub the whole string
            Log.Error(ex, "Error executing debug log scrubber");
            return string.Empty;
        }
    }

#pragma warning disable SA1010 // Opening Square brackets should be preceded by a space
    private static ReplacerConfig[] CreateReplacers() =>
    [
        // yaml comments
        new(GetRegex(@"^\s*#.*$(\r\n|\n)?", options: RegexOptions.Compiled | RegexOptions.Multiline), null, string.Empty),
        // hinted API Key
        new(GetRegex(@"(api_?key=)\b[a-zA-Z0-9]+([a-zA-Z0-9]{5})\b"), ["api_key", "apikey"], "$1***************************$2"),
        // hinted App Key
        new(GetRegex(@"(ap(?:p|plication)_?key=)\b[a-zA-Z0-9]+([a-zA-Z0-9]{5})\b"), ["app_key", "appkey", "application_key"], "$1***********************************$2"),
        // Bearer token
        new(GetRegex(@"\bBearer [a-fA-F0-9]{59}([a-fA-F0-9]{5})\b"), ["Bearer"], "Bearer ***********************************************************$1"),
        // api key YAML
        new(GetRegex(@"(\-|\:|,|\[|\{)(\s+)?\b[a-fA-F0-9]{27}([a-fA-F0-9]{5})\b"), null, "$1$2\"***************************$3\""),
        // apiKey
        new(GetRegex(@"\b[a-fA-F0-9]{27}([a-fA-F0-9]{5})\b"), null, "***************************$1"),
        // app key yaml
        new(GetRegex(@"(\-|\:|,|\[|\{)(\s+)?\b[a-fA-F0-9]{35}([a-fA-F0-9]{5})\b"), null, "$1$2\"***********************************$3\""),
        // app key
        new(GetRegex(@"\b[a-fA-F0-9]{35}([a-fA-F0-9]{5})\b"), null, "***********************************$1"),
        // rc app key
        new(GetRegex(@"\bDDRCM_[A-Z0-9]+([A-Z0-9]{5})\b"), null, "***********************************$1"),
        // URI Generic Syntax
        // https://tools.ietf.org/html/rfc3986
        new(GetRegex(@"(?i)([a-z][a-z0-9+-.]+://|\b)([^:]+):([^\s|""]+)@"), null, "$1$2:********@"),
        // YAML replacers - these are _meant_ to "decode" yaml and treat it as a data object
        // but given we don't expect to actually have any yaml or json in the logs that needs
        // redacting, these follow a more simplistic approach, _based_ on the originals
        // yaml key password
        new(GetRegex(@"(\s*(\w|_)*(pass(word)?|pwd)(\w|_)*\s*:).+"), ["pass", "pwd"], "$1 \"********\""),
        // yaml token
        new(GetRegex(@"(^\s*(\w|_)*token\s*:).+", options: RegexOptions.Compiled | RegexOptions.Multiline), ["token"], "$1 \"********\""),
        // yaml snmp
        new(GetRegex(@"(\s*(community_string|authKey|privKey|community|authentication_key|privacy_key)\s*:).+"), ["community_string", "authKey", "privKey", "community", "authentication_key", "privacy_key"], "$1 \"********\""),
        // yaml apikey - this _only_ works on YAML, so excluded as we don't want to decode
        // yaml appkey - this _only_ works on YAML, so excluded as we don't want to decode

        // yaml snmp multiline
        // matches YAML keys with array values.
        // caveat: doesn't work if the array contain nested arrays.
        //
        // Example:
        //
        //  key: [
        //   [a, b, c],
        //   def]
        new(GetRegex(@"(\s*(community_strings)\s*:)\s*(?:\n(?:\s+-\s+.*)*|\[(?:\n?.*?)*?\])"), ["community_strings"], "$1 \"********\""),
        /*                                 -------------------------      ---------------  -------------
                                                      match key(s)            |                |
                                                                              match multiple   match anything
                                                                              lines starting   enclosed between `[` and `]`
                                                                              with `-` */
        // cert - Try to match as accurately as possible. RFC 7468's ABNF
        // But ignores backreferences
        new(GetRegex(@"-----BEGIN (?:.*)-----[A-Za-z0-9=\+\/\s]*-----END (?:.*)-----"), ["BEGIN"], "********"),
    ];

    private static Regex GetRegex([StringSyntax(StringSyntaxAttribute.Regex)] string pattern, RegexOptions options = RegexOptions.Compiled)
        => new(
            pattern,
#if NETCOREAPP3_1_OR_GREATER
            options | RegexOptions.NonBacktracking,
#else
            options,
#endif
            TimeSpan.FromSeconds(RegexTimeoutSeconds));

    private class ReplacerConfig(Regex regex, string[]? hints, string replacement)
    {
        /// <summary>
        /// Gets the regex to apply to the line
        /// </summary>
        public Regex Regex { get; } = regex;

        /// <summary>
        /// Gets the strings which must also be present in the text for the regexp to match.
        /// Especially in single-line replacers, this can be used to limit the contexts where an otherwise
        /// very broad Regex is actually replaced.
        /// </summary>
        public string[]? Hints { get; } = hints;

        /// <summary>
        /// Gets the text to replace the substring matching Regex. It can use regex replace characters
        /// </summary>
        public string Replacement { get; } = replacement;
    }
}
