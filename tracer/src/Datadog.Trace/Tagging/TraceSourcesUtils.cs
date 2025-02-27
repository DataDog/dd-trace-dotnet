// <copyright file="TraceSourcesUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Tagging
{
    /// <summary>
    /// Utility funtions to handle TraceSources values in the '_dd.p.ts' tag
    /// </summary>
    internal static class TraceSourcesUtils
    {
        public static TraceSources GetTraceSources(this TraceTagCollection? tags)
        {
            if (tags == null)
            {
                return TraceSources.None;
            }

            return GetTraceSources(tags.GetTag(Tags.Propagated.TraceSource));
        }

        public static TraceTagCollection? EnableTraceSources(this TraceTagCollection? tags, TraceSources sources)
        {
            if (tags != null)
            {
                var value = GetTraceSources(tags.GetTag(Tags.Propagated.TraceSource)) | sources;
                if (value != TraceSources.None)
                {
                    tags.SetTag(Tags.Propagated.TraceSource, ToTag(value));
                }
            }

            return tags;
        }

        public static bool HasTraceSources(this TraceTagCollection? tags)
        {
            return GetTraceSources(tags) != TraceSources.None;
        }

        public static bool HasTraceSources(this TraceTagCollection? tags, TraceSources sources)
        {
            return GetTraceSources(tags).HasFlagFast(sources);
        }

        private static TraceSources GetTraceSources(string? traceSources)
        {
            var result = TraceSources.None;

            if (string.IsNullOrEmpty(traceSources) ||
                !int.TryParse(traceSources, System.Globalization.NumberStyles.HexNumber, null, out var hexValue))
            {
                return result;
            }

            // Skip None value
            var values = TraceSourcesExtensions.GetValues();
            for (int x = 1; x < values.Length; x++)
            {
                var value = (int)values[x];
                if ((hexValue | value) == value)
                {
                    result |= (TraceSources)value;
                }
            }

            return result;
        }

        private static string ToTag(TraceSources sources)
        {
            return ((int)sources).ToString("X2");
        }
    }
}
