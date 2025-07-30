// <copyright file="Tracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Stubs;

namespace Datadog.Trace
{
    /// <summary>
    /// The tracer is responsible for creating spans and flushing them to the Datadog agent
    /// </summary>
    public sealed class Tracer : ITracer, IDatadogOpenTracingTracer
    {
        private static Tracer? _instance;
        // we use this object to track when they have changed, so we know whether or not we need to update the values
        private object? _automaticSettings;
        private ImmutableTracerSettings? _settings;

        [Instrumented] // This is only _actually_ instrumented up to 3.6.0 (automatic)
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Tracer(object? automaticTracer, Dictionary<string, object?> initialValues)
        {
            AutomaticTracer = automaticTracer;

            // In 3.7.0+ we don't bother populating the dictionary with the settings
            // seeing as the whole _settings object is going to be thrown away and
            // repopulated if the customer calls Settings. Kind of annoying...
            // so to avoid the extra work/allocation we check whether the initial values contains the
            // agent key, as an easy way of detecting if we're running in this version-conflict mode.
            // If we're not in version conflict, we can delay allocating the settings object until
            // its actually requested.
            if (initialValues.ContainsKey(TracerSettingKeyConstants.AgentUriKey))
            {
                _settings = new ImmutableTracerSettings(initialValues);
            }
        }

        // Not null when the automatic tracer is available
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

                // check that the automatic tracer is still the current instance
                // very unlikely to change, but not impossible
                if (current is not null && current.AutomaticTracer == automaticTracer)
                {
                    // they're the same, nothing more to do
                    return current;
                }

                // need a new tracer instance, because either the automatic tracer has changed
                // or this is the first time fetching it
                var instance = new Tracer(automaticTracer, new());
                _instance = instance;
                return instance;
            }
        }

        /// <summary>
        /// Gets the active scope
        /// </summary>
        [Instrumented]
        public IScope? ActiveScope
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get => null;
        }

        /// <summary>
        /// Gets the default service name for traces where a service name is not specified.
        /// </summary>
        [Instrumented]
        public string DefaultServiceName
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            get => string.Empty;
        }

        /// <summary>
        /// Gets this tracer's settings.
        /// </summary>
        public ImmutableTracerSettings Settings
        {
            get
            {
                // check to see if the settings have changed since last time
                var settings = GetUpdatedImmutableTracerSettings(AutomaticTracer, ref _automaticSettings);

                if (settings is not null)
                {
                    // the settings have changed
                    _settings = new ImmutableTracerSettings(settings);
                }
                else if (_settings is null)
                {
                    // in manual only mode
                    _settings = new ImmutableTracerSettings(new Dictionary<string, object?>());
                }

                return _settings;
            }
        }

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
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IScope StartActive(string operationName)
            => StartActive(operationName, parent: null, serviceName: null, null, finishOnClose: true);

        /// <inheritdoc cref="ITracer" />
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public IScope StartActive(string operationName, SpanCreationSettings settings)
            => StartActive(operationName, settings.Parent, serviceName: null, settings.StartTime, settings.FinishOnClose);

        /// <summary>
        /// Forces the tracer to immediately flush pending traces and send them to the agent.
        /// To be called when the appdomain or the process is about to be killed in a non-graceful way.
        /// </summary>
        /// <returns>Task used to track the async flush operation</returns>
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Task ForceFlushAsync() => Task.CompletedTask;

        /// <summary>
        /// Automatic instrumentation intercepts this method and reconfigures the automatic tracer
        /// </summary>
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Configure(Dictionary<string, object?> settings)
        {
            _ = settings;
        }

        /// <summary>
        /// Automatic instrumentation intercepts this method and returns the global tracer instance
        /// </summary>
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object? GetAutomaticTracerInstance() => null;

        /// <summary>
        /// Automatic instrumentation intercepts this method and returns a dictionary populated with updated
        /// settings, only if the ImmutableTracerSettings (automatic) provided is different to the current one.
        /// </summary>
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private IDictionary<string, object?>? GetUpdatedImmutableTracerSettings(object? automaticTracer, ref object? automaticSettings)
        {
            _ = automaticTracer;
            _ = automaticSettings;
            return null;
        }

        /// <summary>
        /// Automatic instrumentation intercepts this method and returns a duck-typed Scope from Datadog.Trace.
        /// </summary>
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private IScope StartActive(string operationName, ISpanContext? parent, string? serviceName, DateTimeOffset? startTime, bool? finishOnClose)
            => NullScope.Instance;

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
        [MethodImpl(MethodImplOptions.NoInlining)]
        ISpan IDatadogOpenTracingTracer.StartSpan(string operationName, ISpanContext? parent, string serviceName, DateTimeOffset? startTime, bool ignoreActiveScope)
            => NullSpan.Instance;
    }
}
