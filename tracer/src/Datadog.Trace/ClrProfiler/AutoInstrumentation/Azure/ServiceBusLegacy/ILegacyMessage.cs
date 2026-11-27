// <copyright file="ILegacyMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBusLegacy;

internal interface ILegacyMessage : IDuckType
{
    byte[]? Body { get; }

    IDictionary<string, object>? UserProperties { get; }

    ILegacySystemProperties SystemProperties { get; }
}
