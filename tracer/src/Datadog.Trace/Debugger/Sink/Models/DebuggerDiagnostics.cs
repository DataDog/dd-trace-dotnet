// <copyright file="DebuggerDiagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Converters;

namespace Datadog.Trace.Debugger.Sink.Models
{
    internal record DebuggerDiagnostics([property: JsonProperty("diagnostics")] Diagnostics Diagnostics)
    {
    }
}
