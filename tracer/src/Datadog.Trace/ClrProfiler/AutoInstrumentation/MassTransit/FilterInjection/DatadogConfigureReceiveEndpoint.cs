// <copyright file="DatadogConfigureReceiveEndpoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.FilterInjection;

/// <summary>
/// Implementation of MassTransit's IConfigureReceiveEndpoint interface via reverse duck typing.
/// This class is called by MassTransit for every receive endpoint on all transports,
/// allowing us to inject Datadog filters in a transport-agnostic way.
/// </summary>
public sealed class DatadogConfigureReceiveEndpoint
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DatadogConfigureReceiveEndpoint>();

    /// <summary>
    /// Called by MassTransit to configure each receive endpoint.
    /// This is called prior to any consumer, saga, or activity configuration.
    /// </summary>
    /// <param name="name">The endpoint name</param>
    /// <param name="configurator">The receive endpoint configurator (IReceiveEndpointConfigurator)</param>
    [DuckReverseMethod(ParameterTypeNames = new[] { "System.String", "MassTransit.IReceiveEndpointConfigurator, MassTransit" })]
    public void Configure(string name, object configurator)
    {
        if (configurator == null)
        {
            Log.Debug("DatadogConfigureReceiveEndpoint: Configurator is null for endpoint {EndpointName}", name);
            return;
        }

        Log.Debug("DatadogConfigureReceiveEndpoint: Configuring endpoint {EndpointName} with {ConfiguratorType}", name, configurator.GetType().FullName);

        // Inject consume filter via IConsumePipeConfigurator.AddPipeSpecification
        InjectConsumeFilter(configurator);

        // Note: Send and Publish filters cannot be injected at the receive endpoint level because
        // IReceiveEndpointConfigurator doesn't expose AddPipeSpecification for SendContext/PublishContext.
        // However, publish operations are still captured by the underlying transport instrumentation
        // (e.g., RabbitMQ's basic.publish). For MassTransit-specific publish spans, we would need to
        // hook at the bus level via IBusControl or IPublishEndpoint.Publish.
    }

    private static void InjectConsumeFilter(object configurator)
    {
        var pipeSpecification = MassTransitCommon.CreateDatadogConsumePipeSpecification(new DatadogConsumePipeSpecification());
        if (pipeSpecification == null)
        {
            Log.Debug("DatadogConfigureReceiveEndpoint: Could not create consume pipe specification");
            return;
        }

        var addPipeSpecMethod = MassTransitCommon.FindAddPipeSpecificationMethod(configurator.GetType());
        if (addPipeSpecMethod == null)
        {
            Log.Debug("DatadogConfigureReceiveEndpoint: Could not find AddPipeSpecification method");
            return;
        }

        addPipeSpecMethod.Invoke(configurator, new[] { pipeSpecification });
        Log.Debug("DatadogConfigureReceiveEndpoint: Successfully injected consume pipe specification");
    }
}
