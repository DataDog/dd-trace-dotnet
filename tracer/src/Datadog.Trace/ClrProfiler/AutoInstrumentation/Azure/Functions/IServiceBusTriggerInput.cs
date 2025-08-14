// <copyright file="IServiceBusTriggerInput.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    /// <summary>
    /// Duck type interface for Microsoft.Azure.WebJobs.ServiceBus.ServiceBusTriggerInput
    /// </summary>
    internal interface IServiceBusTriggerInput : IDuckType
    {
        /// <summary>
        /// Gets the ReceiveActions property which is of type ServiceBusReceiveActions
        /// </summary>
        IServiceBusReceiveActions? ReceiveActions { get; }
    }
}
