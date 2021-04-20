using System;
using System.Collections.Generic;

namespace Datadog.Trace.Tagging
{
    internal interface ITags
    {
        string GetTag(string key);

        void SetTag(string key, string value);

        double? GetMetric(string key);

        void SetMetric(string key, double? value);

        int SerializeTo(ref byte[] buffer, int offset, Span span, Func<Span, KeyValuePair<string, string>>[] tagFactories, Func<Span, KeyValuePair<string, double?>>[] metricsFactories);
    }
}
