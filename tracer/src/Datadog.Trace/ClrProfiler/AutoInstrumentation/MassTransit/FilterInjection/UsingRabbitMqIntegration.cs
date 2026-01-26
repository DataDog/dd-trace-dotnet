// <copyright file="UsingRabbitMqIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Delegates;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.FilterInjection;

/// <summary>
/// MassTransit IBusRegistrationConfigurator.UsingRabbitMq calltarget instrumentation
/// This hooks into the UsingRabbitMq configuration method to inject Datadog filters during configuration time.
/// </summary>
[InstrumentMethod(
    AssemblyName = "MassTransit.RabbitMqTransport",
    TypeName = "MassTransit.RabbitMqBusFactoryConfiguratorExtensions",
    MethodName = "UsingRabbitMq",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["MassTransit.IBusRegistrationConfigurator", "System.Action`2[MassTransit.IBusRegistrationContext,MassTransit.RabbitMqTransport.IRabbitMqBusFactoryConfigurator]"],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = MassTransitConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class UsingRabbitMqIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(UsingRabbitMqIntegration));
    private static readonly ConfigureCallbacks Callbacks = new();

    /// <summary>
    /// OnMethodBegin callback - wraps the configuration callback to inject filters
    /// </summary>
    /// <typeparam name="TTarget">Type of the target (static extension method class)</typeparam>
    /// <typeparam name="TConfigurator">Type of the configurator (IBusRegistrationConfigurator)</typeparam>
    /// <param name="instance">Instance value (null for static methods)</param>
    /// <param name="configurator">The bus registration configurator (first parameter of extension method)</param>
    /// <param name="configure">The configuration callback to wrap.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TConfigurator>(TTarget instance, TConfigurator configurator, ref Delegate configure)
    {
        Log.Debug("MassTransit UsingRabbitMqIntegration.OnMethodBegin() - Wrapping configuration callback");

        if (configure == null)
        {
            Log.Debug("MassTransit UsingRabbitMqIntegration - Configure delegate is null, skipping");
            return CallTargetState.GetDefault();
        }

        configure = configure.Instrument(Callbacks);

        return CallTargetState.GetDefault();
    }

    private readonly struct ConfigureCallbacks : IBegin2Callbacks, IVoidReturnCallback
    {
        public object? OnDelegateBegin<TArg1, TArg2>(object? sender, ref TArg1 context, ref TArg2 configurator)
        {
            Log.Debug("MassTransit UsingRabbitMqIntegration.ConfigureCallbacks.OnDelegateBegin() - Injecting filter into configurator");

            if (configurator != null)
            {
                MassTransitFilterInjector.InjectConsumeFilter(configurator);
            }

            return null;
        }

        public void OnDelegateEnd(object? sender, Exception? exception, object? state)
        {
            if (exception != null)
            {
                Log.Warning(exception, "MassTransit UsingRabbitMqIntegration - Original configure callback threw an exception");
            }
        }

        public void OnException(object? sender, Exception ex)
        {
            Log.Warning(ex, "MassTransit UsingRabbitMqIntegration - Exception in filter injection");
        }
    }
}
