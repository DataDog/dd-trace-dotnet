// <copyright file="Tracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Tracer;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Internal;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Stubs;

namespace Datadog.Trace
{
    /// <summary>
    /// The tracer is responsible for creating spans and flushing them to the Datadog agent
    /// </summary>
    public sealed class Tracer : ITracer, IDatadogOpenTracingTracer, ITracerProxy
    {
        private static Tracer? _instance;

        [Instrumented]
        private Tracer(object? automaticTracer, Dictionary<string, object?> initialValues)
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                CtorIntegration.OnMethodBegin<Tracer>(this, automaticTracer, initialValues);
            }

            AutomaticTracer = automaticTracer;
            Settings = new ImmutableTracerSettings(initialValues);
        }

        [DuckTypeTarget]
        private object? AutomaticTracer { get; }

        /// <summary>
        /// Gets the global <see cref="Tracer"/> instance to use for manual instrumentation.
        /// </summary>
        public static Tracer Instance
        {
            get
            {
                var automaticTracer = GetAutomaticTracerInstance();
                var current = Volatile.Read(ref _instance);

                if (current is not null)
                {
                    // check that the automatic tracer is still the current instance
                    if (current.AutomaticTracer == automaticTracer)
                    {
                        // they're the same, nothing more to do
                        return current;
                    }
                }

                // need a new tracer instance, because either the automatic tracer has changed
                // or this is the first time fetching it
                var instance = new Tracer(automaticTracer, new());
                _instance = instance;
                return instance;
            }
        }

        /// <summary>
        /// Gets the AutomaticTracer
        /// </summary>
        object ITracerProxy.AutomaticTracer => AutomaticTracer!;

        /// <summary>
        /// Gets the active scope
        /// </summary>
        [Instrumented]
        public IScope? ActiveScope
        {
            get
            {
                if (!Instrumentation.IsAutomaticInstrumentationEnabled())
                {
                    return GetActiveScopeIntegration.OnMethodEnd<Tracer, IScope>(this, default!, default!, default).GetReturnValue();
                }

                return default;
            }
        }

        /// <summary>
        /// Gets the default service name for traces where a service name is not specified.
        /// </summary>
        [Instrumented]
        public string DefaultServiceName
        {
            get
            {
                if (!Instrumentation.IsAutomaticInstrumentationEnabled())
                {
                    return GetDefaultServiceNameIntegration.OnMethodEnd(this, default!, default!, default).GetReturnValue()!;
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Gets this tracer's settings.
        /// </summary>
        public ImmutableTracerSettings Settings { get; }

        /// <summary>
        /// Replaces the global Tracer settings used by all <see cref="Tracer"/> instances,
        /// including automatic instrumentation
        /// </summary>
        /// <param name="settings"> A <see cref="TracerSettings"/> instance with the desired settings,
        /// or null to use the default configuration sources. This is used to configure global settings</param>
        public static void Configure(TracerSettings settings)
        {
            Configure(settings.ToDictionary());
        }

        /// <inheritdoc cref="ITracer" />
        [Instrumented]
        public IScope StartActive(string operationName)
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                StartActiveOperationNameIntegration.OnMethodBegin(this, ref operationName);
            }

            return StartActive(operationName, parent: null, serviceName: null, null, finishOnClose: true);
        }

        /// <inheritdoc cref="ITracer" />
        [Instrumented]
        public IScope StartActive(string operationName, SpanCreationSettings settings)
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                StartActiveSpanCreationSettingsIntegration.OnMethodBegin(this, operationName, settings);
            }

            return StartActive(operationName, settings.Parent, serviceName: null, settings.StartTime, settings.FinishOnClose);
        }

        /// <summary>
        /// Forces the tracer to immediately flush pending traces and send them to the agent.
        /// To be called when the appdomain or the process is about to be killed in a non-graceful way.
        /// </summary>
        /// <returns>Task used to track the async flush operation</returns>
        [Instrumented]
        public Task ForceFlushAsync()
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                return ForceFlushAsyncIntegration.OnMethodEnd(this, default!, default!, default).GetReturnValue()!;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Automatic instrumentation intercepts this method and reconfigures the automatic tracer
        /// </summary>
        [Instrumented]
        private static void Configure(Dictionary<string, object?> settings)
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                ConfigureIntegration.OnMethodBegin<Tracer>(settings);
            }

            _ = settings;
        }

        /// <summary>
        /// Automatic instrumentation intercepts this method and returns the global tracer instance
        /// </summary>
        [Instrumented]
        private static object? GetAutomaticTracerInstance()
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                return GetAutomaticTracerInstanceIntegration.OnMethodEnd<Tracer>(default!, default!, default).GetReturnValue();
            }

            return default;
        }

        /// <summary>
        /// Automatic instrumentation intercepts this method and returns a duck-typed Scope from Datadog.Trace.
        /// </summary>
        [Instrumented]
        private IScope StartActive(string operationName, ISpanContext? parent, string? serviceName, DateTimeOffset? startTime, bool? finishOnClose)
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                var state = StartActiveImplementationIntegration.OnMethodBegin(this, operationName, parent, serviceName, startTime, finishOnClose);
                return StartActiveImplementationIntegration.OnMethodEnd(this, (IScope?)null, default, state).GetReturnValue()!;
            }

            return NullScope.Instance;
        }

        /// <summary>
        /// Creates a new <see cref="ISpan"/> with the specified parameters.
        /// </summary>
        /// <param name="operationName">The span's operation name</param>
        /// <param name="parent">The span's parent</param>
        /// <param name="serviceName">The span's service name</param>
        /// <param name="startTime">An explicit start time for that span</param>
        /// <param name="ignoreActiveScope">If set the span will not be a child of the currently active span</param>
        /// <returns>The newly created span</returns>
        [Instrumented]
        ISpan IDatadogOpenTracingTracer.StartSpan(string operationName, ISpanContext? parent, string serviceName, DateTimeOffset? startTime, bool ignoreActiveScope)
        {
            if (!Instrumentation.IsAutomaticInstrumentationEnabled())
            {
                var state = StartSpanIntegration.OnMethodBegin(this, operationName, parent, serviceName, startTime, ignoreActiveScope);
                return StartSpanIntegration.OnMethodEnd<Tracer, ISpan>(this, default!, default!, state).GetReturnValue()!;
            }

            return NullSpan.Instance;
        }
    }
}
