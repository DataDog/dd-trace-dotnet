// <copyright file="KbSizeResultConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Crank
{
    internal readonly struct KbSizeResultConverter : IResultConverter
    {
        public readonly string Name;

        public KbSizeResultConverter(string name)
        {
            Name = name;
        }

        public bool CanConvert(string name)
            => Name == name;

        public void SetToSpan(Span span, string sanitizedName, object value)
        {
            if (double.TryParse(value.ToString(), out var doubleValue))
            {
                span.SetMetric(sanitizedName + "_bytes", doubleValue * 1024);
            }
            else
            {
                span.SetTag(sanitizedName, value.ToString());
            }
        }
    }
}
