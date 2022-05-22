// <copyright file="ProbeException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger.Sink.Models
{
    internal record ProbeException
    {
        public string Type { get; set; }

        public string Message { get; set; }

        public string StackTrace { get; set; }
    }
}
