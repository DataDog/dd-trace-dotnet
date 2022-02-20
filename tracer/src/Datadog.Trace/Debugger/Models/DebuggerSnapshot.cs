// <copyright file="DebuggerSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Debugger
{
    internal class DebuggerSnapshot
    {
        public string Id { get; set; }

        public string Timestamp { get; set; }

        public string[] Tags { get; set; }

        public string Host { get; set; }

        public string Service { get; set; }

        public string Message { get; set; }

        public ProbeSnapshot ProbeSnapshot { get; set; }
    }
}
