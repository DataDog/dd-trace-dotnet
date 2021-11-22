// <copyright file="IHeaderNormalizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Headers
{
    internal interface IHeaderNormalizer
    {
        /// <summary>
        /// Datadog tag requirements:
        /// 1. Tag must start with a letter
        /// 2. Tag cannot exceed 200 characters
        /// 3. If the first two requirements are met, then valid characters will be retained while all other characters will be converted to underscores. Valid characters include:
        ///    - Alphanumerics
        ///    - Underscores
        ///    - Minuses
        ///    - Colons
        ///    - Periods
        ///    - Slashes
        ///
        /// Note: This method will trim leading/trailing whitespace before checking the requirements.
        /// </summary>
        /// <param name="value">Input string to convert into tag name</param>
        /// <param name="normalizedTagName">If the method returns true, the normalized tag name</param>
        /// <returns>Returns whether the conversion was successful</returns>
        bool TryConvertToNormalizedTagName(string value, out string normalizedTagName);

        /// <summary>
        /// Attempts to convert the input to a valid tag name following the rules
        /// described in <see cref="TryConvertToNormalizedTagName(string, out string)"/>
        ///
        /// Additionally, the resulting tag name replaces any periods with underscores so that
        /// the tag does not create any further object nesting.
        /// </summary>
        /// <param name="value">Input string to convert into tag name</param>
        /// <param name="normalizedTagName">If the method returns true, the normalized header tag name</param>
        /// <returns>Returns whether the conversion was successful</returns>
        bool TryConvertToNormalizedTagNameIncludingPeriods(string value, out string normalizedTagName);
    }
}
