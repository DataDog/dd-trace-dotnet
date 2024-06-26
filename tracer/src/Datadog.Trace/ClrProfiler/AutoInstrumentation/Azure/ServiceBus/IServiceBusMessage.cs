// <copyright file="IServiceBusMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;

internal interface IServiceBusMessage : IDuckType
{
    IDictionary<string, object> ApplicationProperties { get; }

    IBinaryData Body { get; }
}
