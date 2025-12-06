// <copyright file="IDatadogLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging.Internal.Configuration;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging
{
    internal interface IDatadogLogger
    {
        public FileLoggingConfiguration? FileLoggingConfiguration { get; }

        bool IsEnabled(LogEventLevel level);

        void Debug(string messageTemplate);

        void Debug<T>(string messageTemplate, T property);

        void Debug<T0, T1>(string messageTemplate, T0 property0, T1 property1);

        void Debug<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Debug<T0, T1, T2, T3>(string messageTemplate, T0 property0, T1 property1, T2 property2, T3 property3);

        void Debug(string messageTemplate, object?[] args);

        void Debug(Exception? exception, string messageTemplate);

        void Debug<T>(Exception? exception, string messageTemplate, T property);

        void Debug<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1);

        void Debug<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Debug(Exception? exception, string messageTemplate, object?[] args);

        void Information(string messageTemplate);

        void Information<T>(string messageTemplate, T property);

        void Information<T0, T1>(string messageTemplate, T0 property0, T1 property1);

        void Information<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Information(string messageTemplate, object?[] args);

        void Information(Exception? exception, string messageTemplate);

        void Information<T>(Exception? exception, string messageTemplate, T property);

        void Information<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1);

        void Information<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Information(Exception? exception, string messageTemplate, object?[] args);

        void Warning(string messageTemplate);

        void Warning<T>(string messageTemplate, T property);

        void Warning<T0, T1>(string messageTemplate, T0 property0, T1 property1);

        void Warning<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Warning(string messageTemplate, object?[] args);

        void Warning(Exception? exception, string messageTemplate);

        void Warning<T>(Exception? exception, string messageTemplate, T property);

        void Warning<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1);

        void Warning<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Warning(Exception? exception, string messageTemplate, object?[] args);

        void Error(string messageTemplate);

        void Error<T>(string messageTemplate, T property);

        void Error<T0, T1>(string messageTemplate, T0 property0, T1 property1);

        void Error<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Error(string messageTemplate, object?[] args);

        void Error(Exception? exception, string messageTemplate);

        void Error<T>(Exception? exception, string messageTemplate, T property);

        void Error<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1);

        void Error<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Error(Exception? exception, string messageTemplate, object?[] args);

        void ErrorSkipTelemetry(string messageTemplate);

        void ErrorSkipTelemetry<T>(string messageTemplate, T property);

        void ErrorSkipTelemetry<T0, T1>(string messageTemplate, T0 property0, T1 property1);

        void ErrorSkipTelemetry<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2);

        void ErrorSkipTelemetry(string messageTemplate, object?[] args);

        void ErrorSkipTelemetry(Exception? exception, string messageTemplate);

        void ErrorSkipTelemetry<T>(Exception? exception, string messageTemplate, T property);

        void ErrorSkipTelemetry<T0, T1>(Exception? exception, string messageTemplate, T0 property0, T1 property1);

        void ErrorSkipTelemetry<T0, T1, T2>(Exception? exception, string messageTemplate, T0 property0, T1 property1, T2 property2);

        void ErrorSkipTelemetry(Exception? exception, string messageTemplate, object?[] args);

        void CloseAndFlush();
    }
}
