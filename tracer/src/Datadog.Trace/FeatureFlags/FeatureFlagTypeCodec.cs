// <copyright file="FeatureFlagTypeCodec.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Datadog.Trace.FeatureFlags
{
    internal static class FeatureFlagTypeCodec
    {
        internal static Dictionary<string, char>? Parse(string encoded)
        {
            Dictionary<string, char>? types = null;
            var position = 0;
            while (position < encoded.Length)
            {
                var colon = encoded.IndexOf(':', position);
                if (colon <= position
                 || !TryParseLength(encoded, position, colon - position, out var length)
                 || length > encoded.Length - colon - 2)
                {
                    break;
                }

                var key = encoded.Substring(colon + 1, length);
                var type = encoded[colon + 1 + length];
                position = colon + 2 + length;
                if (type is 'b' or 'n')
                {
                    (types ??= new Dictionary<string, char>(StringComparer.Ordinal))[key] = type;
                }
            }

            return types;
        }

        private static bool TryParseLength(string encoded, int start, int length, out int value)
        {
#if NETCOREAPP3_0_OR_GREATER
            return int.TryParse(encoded.AsSpan(start, length), NumberStyles.None, CultureInfo.InvariantCulture, out value);
#else
            return int.TryParse(encoded.Substring(start, length), NumberStyles.None, CultureInfo.InvariantCulture, out value);
#endif
        }
    }
}
