// <copyright file="IResultConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1201 // Elements should appear in the correct order

namespace Datadog.Trace.Tools.Runner.Crank
{
    internal interface IResultConverter
    {
        bool CanConvert(string name);

        void SetToSpan(Span span, string sanitizedName, object value);
    }
}
