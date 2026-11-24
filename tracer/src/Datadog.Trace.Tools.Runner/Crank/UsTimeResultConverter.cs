// <copyright file="UsTimeResultConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tools.Runner.Crank
{
    internal readonly struct UsTimeResultConverter : IResultConverter
    {
        public readonly string Name;

        public UsTimeResultConverter(string name)
        {
            Name = name;
        }

        public bool CanConvert(string name)
            => Name == name;

        public void SetToSpan(Span span, string sanitizedName, object value)
        {
            if (ResultValue.TryGetDouble(value, out var doubleValue))
            {
                span.SetMetric(sanitizedName + "_μs", doubleValue * 1_000);
            }
            else
            {
                span.SetTag(sanitizedName, value.ToString());
            }
        }
    }
}
