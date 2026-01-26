// <copyright file="DatadogConfigureReceiveEndpoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.FilterInjection;

/// <summary>
/// Implementation of MassTransit's IConfigureReceiveEndpoint interface via reverse duck typing.
/// This class is called by MassTransit for every receive endpoint on all transports,
/// allowing us to inject Datadog filters in a transport-agnostic way.
/// </summary>
internal sealed class DatadogConfigureReceiveEndpoint
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DatadogConfigureReceiveEndpoint>();

    /// <summary>
    /// Called by MassTransit to configure each receive endpoint.
    /// This is called prior to any consumer, saga, or activity configuration.
    /// </summary>
    /// <param name="name">The endpoint name</param>
    /// <param name="configurator">The receive endpoint configurator (IReceiveEndpointConfigurator)</param>
    [DuckReverseMethod]
    public void Configure(string name, object configurator)
    {
        if (configurator == null)
        {
            Log.Debug("DatadogConfigureReceiveEndpoint: Configurator is null for endpoint {EndpointName}", name);
            return;
        }

        try
        {
            Log.Debug("DatadogConfigureReceiveEndpoint: Configuring endpoint {EndpointName} with {ConfiguratorType}", name, configurator.GetType().FullName);

            // Inject consume filter via IConsumePipeConfigurator.AddPipeSpecification
            InjectConsumeFilter(configurator);

            // Note: Send and Publish filters cannot be injected at the receive endpoint level because
            // IReceiveEndpointConfigurator doesn't expose AddPipeSpecification for SendContext/PublishContext.
            // However, publish operations are still captured by the underlying transport instrumentation
            // (e.g., RabbitMQ's basic.publish). For MassTransit-specific publish spans, we would need to
            // hook at the bus level via IBusControl or IPublishEndpoint.Publish.
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DatadogConfigureReceiveEndpoint: Failed to configure endpoint {EndpointName}", name);
        }
    }

    private static void InjectConsumeFilter(object configurator)
    {
        try
        {
            var configuratorType = configurator.GetType();

            // Find MassTransit assembly
            var massTransitAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "MassTransit");

            if (massTransitAssembly == null)
            {
                Log.Debug("DatadogConfigureReceiveEndpoint: Could not find MassTransit assembly");
                return;
            }

            // Find GreenPipes assembly
            var greenPipesAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "GreenPipes");

            if (greenPipesAssembly == null)
            {
                Log.Debug("DatadogConfigureReceiveEndpoint: Could not find GreenPipes assembly");
                return;
            }

            // Get ConsumeContext type
            var consumeContextType = massTransitAssembly.GetType("MassTransit.ConsumeContext");
            if (consumeContextType == null)
            {
                Log.Debug("DatadogConfigureReceiveEndpoint: Could not find ConsumeContext type");
                return;
            }

            // Get IPipeSpecification<ConsumeContext> type
            var pipeSpecOpenType = greenPipesAssembly.GetType("GreenPipes.IPipeSpecification`1");
            if (pipeSpecOpenType == null)
            {
                Log.Debug("DatadogConfigureReceiveEndpoint: Could not find IPipeSpecification<> type");
                return;
            }

            var pipeSpecType = pipeSpecOpenType.MakeGenericType(consumeContextType);

            // Create our filter specification via reverse duck typing
            var filterSpecImpl = new DatadogConsumePipeSpecification();
            var filterSpec = DuckType.CreateReverse(pipeSpecType, filterSpecImpl);

            if (filterSpec == null)
            {
                Log.Debug("DatadogConfigureReceiveEndpoint: Could not create reverse duck type for IPipeSpecification<ConsumeContext>");
                return;
            }

            // Find the AddPipeSpecification method on the configurator
            // IConsumePipeConfigurator.AddPipeSpecification(IPipeSpecification<ConsumeContext>)
            var addPipeSpecMethod = configuratorType.GetMethod("AddPipeSpecification", new[] { pipeSpecType });

            if (addPipeSpecMethod == null)
            {
                // Try finding the method on interfaces
                foreach (var iface in configuratorType.GetInterfaces())
                {
                    addPipeSpecMethod = iface.GetMethod("AddPipeSpecification", new[] { pipeSpecType });
                    if (addPipeSpecMethod != null)
                    {
                        break;
                    }
                }
            }

            if (addPipeSpecMethod == null)
            {
                Log.Debug("DatadogConfigureReceiveEndpoint: Could not find AddPipeSpecification method");
                return;
            }

            addPipeSpecMethod.Invoke(configurator, new[] { filterSpec });
            Log.Debug("DatadogConfigureReceiveEndpoint: Successfully injected consume filter specification");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogConfigureReceiveEndpoint: Failed to inject consume filter");
        }
    }
}
