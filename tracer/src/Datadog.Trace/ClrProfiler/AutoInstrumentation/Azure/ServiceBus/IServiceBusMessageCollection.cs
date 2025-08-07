// <copyright file="IServiceBusMessageCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Duck typing interface for ServiceBus message collections
    /// Avoids the Count property that was causing duck typing issues
    /// </summary>
    internal interface IServiceBusMessageCollection : IDuckType, IEnumerable<IServiceBusReceivedMessage>
    {
        // Note: We intentionally don't include Count here to avoid duck typing issues
        // Count will be accessed via reflection in GetCollectionCount method
    }
}
