// <copyright file="IAmqpAnnotatedMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;

/// <summary>
/// Duck type interface for Azure.Core.Amqp.AmqpAnnotatedMessage
/// </summary>
internal interface IAmqpAnnotatedMessage : IDuckType
{
    IDictionary<string, object>? ApplicationProperties { get; }
}
