// <copyright file="UsingInMemoryIntegration.cs" company="Datadog">
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
/// MassTransit IBusRegistrationConfigurator.UsingInMemory calltarget instrumentation
/// This hooks into the UsingInMemory configuration method to inject Datadog filters during configuration time.
///
/// In MassTransit 7.x, the UsingInMemory method is an extension method on IBusRegistrationConfigurator
/// that takes an Action{IBusRegistrationContext, IInMemoryBusFactoryConfigurator} callback.
///
/// We wrap this callback to call MassTransitFilterInjector.InjectConsumeFilter() with the configurator,
/// which adds our filter specification to the _specifications list before the pipeline is compiled.
/// </summary>
[InstrumentMethod(
    AssemblyName = MassTransitConstants.MassTransitAssembly,
    TypeName = "MassTransit.InMemoryConfigurationExtensions",
    MethodName = "UsingInMemory",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["MassTransit.IBusRegistrationConfigurator", "System.Action`2[MassTransit.IBusRegistrationContext,MassTransit.IInMemoryBusFactoryConfigurator]"],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = MassTransitConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class UsingInMemoryIntegration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(UsingInMemoryIntegration));
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
        Log.Debug("MassTransit UsingInMemoryIntegration.OnMethodBegin() - Wrapping configuration callback");

        if (configure == null)
        {
            Log.Debug("MassTransit UsingInMemoryIntegration - Configure delegate is null, skipping");
            return CallTargetState.GetDefault();
        }

        // Wrap the configure delegate using the DelegateInstrumentation infrastructure
        // This creates a wrapper that intercepts the callback invocation
        configure = configure.Instrument(Callbacks);

        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Callbacks for the Action{IBusRegistrationContext, IInMemoryBusFactoryConfigurator} delegate.
    /// Implements IBegin2Callbacks to intercept the 2-argument action before and after invocation.
    /// </summary>
    private readonly struct ConfigureCallbacks : IBegin2Callbacks, IVoidReturnCallback
    {
        /// <summary>
        /// Called before the original delegate is invoked.
        /// This is where we inject the Datadog filter into the configurator.
        /// </summary>
        /// <typeparam name="TArg1">Type of the first argument (IBusRegistrationContext)</typeparam>
        /// <typeparam name="TArg2">Type of the second argument (IInMemoryBusFactoryConfigurator)</typeparam>
        /// <param name="sender">The delegate target</param>
        /// <param name="context">The bus registration context</param>
        /// <param name="configurator">The bus factory configurator</param>
        /// <returns>State object (null)</returns>
        public object? OnDelegateBegin<TArg1, TArg2>(object? sender, ref TArg1 context, ref TArg2 configurator)
        {
            Log.Debug("MassTransit UsingInMemoryIntegration.ConfigureCallbacks.OnDelegateBegin() - Injecting filter into configurator");

            if (configurator != null)
            {
                // Inject our filter BEFORE the original callback executes
                // This ensures our filter is added before ConfigureEndpoints is called
                MassTransitFilterInjector.InjectConsumeFilter(configurator);
            }

            return null;
        }

        /// <summary>
        /// Called after the original delegate completes (for void-returning delegates).
        /// </summary>
        /// <param name="sender">The delegate target</param>
        /// <param name="exception">Exception if one was thrown</param>
        /// <param name="state">State from OnDelegateBegin</param>
        public void OnDelegateEnd(object? sender, Exception? exception, object? state)
        {
            if (exception != null)
            {
                Log.Warning(exception, "MassTransit UsingInMemoryIntegration - Original configure callback threw an exception");
            }
        }

        /// <summary>
        /// Called when an exception occurs in the callbacks themselves.
        /// </summary>
        /// <param name="sender">The delegate target</param>
        /// <param name="ex">The exception</param>
        public void OnException(object? sender, Exception ex)
        {
            Log.Warning(ex, "MassTransit UsingInMemoryIntegration - Exception in filter injection");
        }
    }
}
