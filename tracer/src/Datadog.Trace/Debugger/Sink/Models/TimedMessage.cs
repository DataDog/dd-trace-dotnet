// <copyright file="TimedMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.Sink.Models
{
    internal class TimedMessage
    {
        public DateTime LastEmit { get; set; }

        public ProbeStatus Message { get; set; }
    }
}
