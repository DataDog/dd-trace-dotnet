// <copyright file="ILogEvent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions.Valid;

namespace Datadog.Trace.DuckTyping.Tests.Errors.ReverseProxy.ProxiesDefinitions
{
    public interface ILogEvent
    {
        public DateTimeOffset Timestamp { get; }

        public LogEventLevel Level { get; }

        public IMessageTemplate MessageTemplate { get; }

        public Exception Exception { get; }

        void AddPropertyIfAbsent(ILogEventProperty property);
    }
}
