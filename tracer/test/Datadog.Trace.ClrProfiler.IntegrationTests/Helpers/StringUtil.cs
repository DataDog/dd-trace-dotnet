// <copyright file="StringUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text.RegularExpressions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    internal static class StringUtil
    {
        /// <summary>
        /// Retrieves a header using a regular expression match.
        /// </summary>
        /// <param name="source">Source string to search.</param>
        /// <param name="name">Name of header to search for.</param>
        /// <returns>Matched string or null if no match</returns>
        internal static string GetHeader(string source, string name)
        {
            var pattern = $@"^\[HttpListener\] request header: {name}=(.*?)\r?$";
            var match = Regex.Match(source, pattern, RegexOptions.Multiline);

            return match.Success
                       ? match.Groups[1].Value
                       : null;
        }
    }
}
