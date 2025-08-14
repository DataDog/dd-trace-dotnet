// <copyright file="IServiceBusReceiveActions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    /// <summary>
    /// Duck type interface for Microsoft.Azure.WebJobs.ServiceBus.ServiceBusReceiveActions
    /// </summary>
    internal interface IServiceBusReceiveActions : IDuckType
    {
        /// <summary>
        /// Gets the internal _receiver field which is a ServiceBusReceiver
        /// Note: This accesses a private field and duck types it to IServiceBusReceiver
        /// </summary>
        [DuckField(Name = "_receiver")]
        IServiceBusReceiver? Receiver { get; }
    }
}
