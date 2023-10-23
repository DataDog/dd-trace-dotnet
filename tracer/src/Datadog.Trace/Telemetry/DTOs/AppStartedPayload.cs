// <copyright file="AppStartedPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;

namespace Datadog.Trace.Telemetry;

internal class AppStartedPayload : IPayload
{
    public ProductsData? Products { get; set; }

    public ICollection<ConfigurationKeyValue>? Configuration { get; set; }

    public ErrorData? Error { get; set; }

    public ICollection<TelemetryValue>? AdditionalPayload { get; set; }
}
