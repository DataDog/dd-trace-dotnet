// <copyright file="ILogSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Logging.Emission;

namespace Datadog.Logging.Composition
{
    internal interface ILogSink
    {
        bool TryLogError(LogSourceInfo logSourceInfo, string message, Exception exception, IEnumerable<object> dataNamesAndValues);

        bool TryLogInfo(LogSourceInfo logSourceInfo, string message, IEnumerable<object> dataNamesAndValues);

        bool TryLogDebug(LogSourceInfo logSourceInfo, string message, IEnumerable<object> dataNamesAndValues);
    }
}