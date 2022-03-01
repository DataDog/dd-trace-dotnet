// <copyright file="ITagProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Processors
{
    internal interface ITagProcessor
    {
        void ProcessMeta(ref string key, ref string value);

        void ProcessMetric(ref string key, ref double value);
    }
}
