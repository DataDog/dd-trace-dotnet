// <copyright file="AddMassTransitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.FilterInjection;

/// <summary>
/// MassTransit AddMassTransit calltarget instrumentation.
/// This is a transport-agnostic hook that registers our IConfigureReceiveEndpoint implementation
/// to inject filters into ALL MassTransit transports (RabbitMQ, Azure Service Bus, SQS, etc.)
/// </summary>
[InstrumentMethod(
    AssemblyName = "MassTransit.ExtensionsDependencyInjectionIntegration",
    TypeName = "MassTransit.DependencyInjectionRegistrationExtensions",
    MethodName = "AddMassTransit",
    ReturnTypeName = "Microsoft.Extensions.DependencyInjection.IServiceCollection",
    ParameterTypeNames = ["Microsoft.Extensions.DependencyInjection.IServiceCollection", "System.Action`1[MassTransit.ExtensionsDependencyInjectionIntegration.IServiceCollectionBusConfigurator]"],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = MassTransitConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class AddMassTransitIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AddMassTransitIntegration>();

    /// <summary>
    /// OnMethodBegin callback - stores the IServiceCollection for use in OnMethodEnd
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TCollection">Type of the IServiceCollection</typeparam>
    /// <typeparam name="TAction">Type of the configure action</typeparam>
    /// <param name="collection">The IServiceCollection instance</param>
    /// <param name="configure">The configure action</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TCollection, TAction>(TCollection collection, TAction configure)
    {
        if (!Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(MassTransitConstants.IntegrationId))
        {
            return CallTargetState.GetDefault();
        }

        Log.Debug("MassTransit AddMassTransitIntegration.OnMethodBegin() - Storing IServiceCollection for filter registration");

        // Store the collection for use in OnMethodEnd
        return new CallTargetState(scope: null, state: collection);
    }

    /// <summary>
    /// OnMethodEnd callback - registers our IConfigureReceiveEndpoint implementation.
    /// </summary>
    /// <typeparam name="TTarget">Type of the target class.</typeparam>
    /// <typeparam name="TReturn">Type of the return value (IServiceCollection).</typeparam>
    /// <param name="returnValue">The IServiceCollection returned by AddMassTransit.</param>
    /// <param name="exception">Exception instance if the original method threw.</param>
    /// <param name="state">CallTarget state containing the IServiceCollection from OnMethodBegin.</param>
    /// <returns>A CallTargetReturn wrapping the original return value.</returns>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        if (exception != null || state.State == null)
        {
            return new CallTargetReturn<TReturn>(returnValue);
        }

        try
        {
            var collection = state.State;
            RegisterDatadogConfigureReceiveEndpoint(collection);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MassTransit AddMassTransitIntegration: Failed to register IConfigureReceiveEndpoint");
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }

    private static void RegisterDatadogConfigureReceiveEndpoint(object collection)
    {
        // We need to call: collection.AddScoped<IConfigureReceiveEndpoint, DatadogConfigureReceiveEndpoint>()
        // But we can't reference the types directly, so we use reflection/duck typing

        var collectionType = collection.GetType();

        // Get IConfigureReceiveEndpoint type from MassTransit assembly
        var configureReceiveEndpointType = MassTransitCommon.GetConfigureReceiveEndpointType();
        if (configureReceiveEndpointType == null)
        {
            Log.Debug("MassTransit AddMassTransitIntegration: Could not find IConfigureReceiveEndpoint type");
            return;
        }

        // Create a reverse duck type instance that implements IConfigureReceiveEndpoint
        var datadogProxy = MassTransitCommon.CreateConfigureReceiveEndpointProxy(new DatadogConfigureReceiveEndpoint());
        if (datadogProxy == null)
        {
            Log.Debug("MassTransit AddMassTransitIntegration: Could not create reverse duck type for IConfigureReceiveEndpoint");
            return;
        }

        // Find ServiceDescriptor and ServiceLifetime types from DI assembly
        var serviceDescriptorType = MassTransitCommon.GetServiceDescriptorType();
        if (serviceDescriptorType == null)
        {
            Log.Debug("MassTransit AddMassTransitIntegration: Could not find ServiceDescriptor type");
            return;
        }

        var serviceLifetimeType = MassTransitCommon.GetServiceLifetimeType();
        if (serviceLifetimeType == null)
        {
            Log.Debug("MassTransit AddMassTransitIntegration: Could not find ServiceLifetime type");
            return;
        }

        // Create ServiceDescriptor for our implementation (Scoped lifetime)
        var scopedLifetime = Enum.ToObject(serviceLifetimeType, 1); // ServiceLifetime.Scoped = 1

        // Create a factory func that returns our proxy instance
        var diAssembly = MassTransitCommon.GetDiAbstractionsAssembly();
        var serviceProviderType = diAssembly?.GetType("System.IServiceProvider") ?? typeof(IServiceProvider);
        var funcType = typeof(Func<,>).MakeGenericType(serviceProviderType, configureReceiveEndpointType);

        // Create a delegate that returns our singleton proxy
        var factoryMethod = typeof(AddMassTransitIntegration)
            .GetMethod(nameof(CreateFactoryDelegate), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?
            .MakeGenericMethod(configureReceiveEndpointType);

        if (factoryMethod == null)
        {
            Log.Debug("MassTransit AddMassTransitIntegration: Could not find CreateFactoryDelegate method");
            return;
        }

        var factory = factoryMethod.Invoke(null, new[] { datadogProxy });

        // Find the ServiceDescriptor constructor: ServiceDescriptor(Type serviceType, Func<IServiceProvider, object> factory, ServiceLifetime lifetime)
        var descriptorCtor = serviceDescriptorType.GetConstructor([typeof(Type), funcType, serviceLifetimeType]);
        if (descriptorCtor == null)
        {
            // Try the simpler overload
            descriptorCtor = serviceDescriptorType.GetConstructor([typeof(Type), typeof(object), serviceLifetimeType]);
        }

        if (descriptorCtor == null)
        {
            Log.Debug("MassTransit AddMassTransitIntegration: Could not find ServiceDescriptor constructor");
            return;
        }

        var descriptor = descriptorCtor.Invoke([configureReceiveEndpointType, factory, scopedLifetime]);

        // Add the descriptor to the collection
        // collection.Add(descriptor)
        var addMethod = collectionType.GetMethod("Add", [serviceDescriptorType]);
        if (addMethod == null)
        {
            // Try ICollection<ServiceDescriptor>.Add
            var iCollectionType = typeof(System.Collections.Generic.ICollection<>).MakeGenericType(serviceDescriptorType);
            addMethod = iCollectionType.GetMethod("Add");
        }

        if (addMethod != null)
        {
            addMethod.Invoke(collection, [descriptor]);
            Log.Debug("MassTransit AddMassTransitIntegration: Successfully registered DatadogConfigureReceiveEndpoint");
        }
        else
        {
            Log.Debug("MassTransit AddMassTransitIntegration: Could not find Add method on IServiceCollection");
        }
    }

    private static Func<IServiceProvider, TService> CreateFactoryDelegate<TService>(object instance)
        where TService : class
    {
        return _ => (TService)instance;
    }
}
