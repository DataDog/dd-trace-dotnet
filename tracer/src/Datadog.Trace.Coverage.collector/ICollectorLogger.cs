// <copyright file="ICollectorLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Coverage.Collector
{
    internal interface ICollectorLogger
    {
        void Error(string? text);

        void Error(Exception exception);

        void Error(Exception exception, string? text);

        void Warning(string? text);

        void Debug(string? text);
    }
}
