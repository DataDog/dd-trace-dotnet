// <copyright file="HeaderNormalizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;

namespace Datadog.Trace.Headers
{
    internal class HeaderNormalizer : IHeaderNormalizer
    {
        public bool TryConvertToNormalizedTagName(string value, out string normalizedTagName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                normalizedTagName = null;
                return false;
            }

            var trimmedValue = value.Trim();
            if (!char.IsLetter(trimmedValue[0]) || trimmedValue.Length > 200)
            {
                normalizedTagName = null;
                return false;
            }

            var sb = new StringBuilder(trimmedValue.ToLowerInvariant());

            for (var x = 0; x < sb.Length; x++)
            {
                switch (sb[x])
                {
                    case (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or ':' or '/' or '-' or '.':
                        continue;
                    default:
                        sb[x] = '_';
                        break;
                }
            }

            normalizedTagName = sb.ToString();
            return true;
        }

        public bool TryConvertToNormalizedTagNameIncludingPeriods(string value, out string normalizedTagName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                normalizedTagName = null;
                return false;
            }

            var trimmedValue = value.Trim();
            if (!char.IsLetter(trimmedValue[0]) || trimmedValue.Length > 200)
            {
                normalizedTagName = null;
                return false;
            }

            var sb = new StringBuilder(trimmedValue.ToLowerInvariant());

            for (var x = 0; x < sb.Length; x++)
            {
                switch (sb[x])
                {
                    // Notice that the '.' does not match, differing from TryConvertToNormalizedTagName
                    case (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or ':' or '/' or '-':
                        continue;
                    default:
                        sb[x] = '_';
                        break;
                }
            }

            normalizedTagName = sb.ToString();
            return true;
        }
    }
}
