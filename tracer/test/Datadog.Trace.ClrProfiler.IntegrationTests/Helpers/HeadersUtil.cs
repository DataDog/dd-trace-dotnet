// <copyright file="HeadersUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    internal static class HeadersUtil
    {
        private static readonly Regex HeaderRegex = new(
            @"^\[HttpListener\] request header: (?<name>.*?)=(?<value>.*?)\r?$",
            RegexOptions.Multiline);

        /// <summary>
        /// Retrieves a header using a regular expression match.
        /// </summary>
        /// <param name="source">Source string to search.</param>
        /// <param name="name">Name of header to search for.</param>
        /// <returns>Matched string or null if no match</returns>
        internal static string? GetHeader(string source, string name)
        {
            var pattern = $@"^\[HttpListener\] request header: {name}=(.*?)\r?$";
            var match = Regex.Match(source, pattern, RegexOptions.Multiline);

            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Retrieves all headers using a regular expression match.
        /// </summary>
        /// <param name="source">Source string to search.</param>
        /// <returns>Matched headers.</returns>
        internal static IEnumerable<KeyValuePair<string, string>> GetAllHeaders(string source)
        {
            return HeaderRegex.Matches(source)
                              .Cast<Match>() // required in older runtimes where MatchCollection implements IEnumerable, but not IEnumerable<Match>
                              .Where(m => m.Success)
                              .Select(m => new KeyValuePair<string, string>(
                                  m.Groups["name"].Value,
                                  m.Groups["value"].Value));
        }
    }
}
